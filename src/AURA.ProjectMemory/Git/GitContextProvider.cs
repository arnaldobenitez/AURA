using System.ComponentModel;
using System.Diagnostics;
using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Git;

/// <summary>
/// Captures Git context by shelling out to the real <c>git</c> executable (AURA_PROJECT_MEMORY
/// v1.0 Etapa 6 §8): no native Git library, no shell (<c>cmd.exe</c>/PowerShell), arguments
/// always passed via <see cref="ProcessStartInfo.ArgumentList"/> (never concatenated strings),
/// with a timeout and no network access. Git's own repository discovery (walking up from the
/// working directory to the nearest <c>.git</c>) is reused as-is, so PROJECT_ROOT vs. a parent
/// or nested Git root (§17/§18) is handled correctly for free.
/// </summary>
public static class GitContextProvider
{
    private const int DefaultTimeoutMilliseconds = 5000;

    private static readonly string[] ConflictStatusCodes = ["UU ", "AA ", "DD ", "AU ", "UA ", "UD ", "DU "];

    public static GitContext Capture(
        string path, string gitExecutable = "git", int timeoutMilliseconds = DefaultTimeoutMilliseconds)
    {
        var probe = RunGit(path, gitExecutable, timeoutMilliseconds, "rev-parse", "--is-inside-work-tree");

        if (probe.Status == GitRunStatus.ExecutableNotFound)
        {
            return GitContext.Unavailable([
                new Diagnostic(DiagnosticSeverity.Info, DiagnosticCode.GitUnavailable,
                    "No usable 'git' executable was found. Git metadata will be omitted.")
            ]);
        }

        if (probe.Status != GitRunStatus.Completed || !probe.Success || probe.StdOut.Trim() != "true")
        {
            return GitContext.NotARepository([
                new Diagnostic(DiagnosticSeverity.Info, DiagnosticCode.NotGitRepository,
                    "This path is not inside a Git work tree. Git metadata will be omitted.")
            ]);
        }

        var diagnostics = new List<Diagnostic>();

        var branch = CaptureBranch(path, gitExecutable, timeoutMilliseconds, diagnostics);
        var commit = CaptureCommit(path, gitExecutable, timeoutMilliseconds, diagnostics);
        var (workingTree, hasConflicts) = CaptureWorkingTree(path, gitExecutable, timeoutMilliseconds, diagnostics);

        return new GitContext(GitAvailability.Available, branch, commit, workingTree, hasConflicts, diagnostics);
    }

    private static string? CaptureBranch(
        string path, string gitExecutable, int timeoutMilliseconds, List<Diagnostic> diagnostics)
    {
        var result = RunGit(path, gitExecutable, timeoutMilliseconds, "rev-parse", "--abbrev-ref", "HEAD");
        if (result.Status != GitRunStatus.Completed)
        {
            diagnostics.Add(CommandFailedDiagnostic("determine the current branch"));
            return null;
        }

        if (!result.Success)
        {
            // No commits yet, or another expected edge case - not worth surfacing as a diagnostic.
            return null;
        }

        var value = result.StdOut.Trim();
        // "HEAD" is what Git reports for a detached HEAD; never invent a branch name for it.
        return value == "HEAD" ? null : value;
    }

    private static string? CaptureCommit(
        string path, string gitExecutable, int timeoutMilliseconds, List<Diagnostic> diagnostics)
    {
        var result = RunGit(path, gitExecutable, timeoutMilliseconds, "rev-parse", "HEAD");
        if (result.Status != GitRunStatus.Completed)
        {
            diagnostics.Add(CommandFailedDiagnostic("determine the current commit"));
            return null;
        }

        // A repository with no commits yet also reports failure here; that is a normal, silent case.
        return result.Success ? result.StdOut.Trim() : null;
    }

    private static (WorkingTreeStatus, bool HasConflicts) CaptureWorkingTree(
        string path, string gitExecutable, int timeoutMilliseconds, List<Diagnostic> diagnostics)
    {
        var result = RunGit(path, gitExecutable, timeoutMilliseconds, "status", "--porcelain");
        if (result.Status != GitRunStatus.Completed || !result.Success)
        {
            if (result.Status != GitRunStatus.Completed)
            {
                diagnostics.Add(CommandFailedDiagnostic("determine the working tree status"));
            }

            return (WorkingTreeStatus.Unknown, false);
        }

        var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hasConflicts = lines.Any(line => ConflictStatusCodes.Any(code => line.StartsWith(code, StringComparison.Ordinal)));

        if (hasConflicts)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, DiagnosticCode.GitMergeConflict,
                "Git repository contains unresolved merge conflicts."));
        }

        return (lines.Length == 0 ? WorkingTreeStatus.Clean : WorkingTreeStatus.Dirty, hasConflicts);
    }

    private static Diagnostic CommandFailedDiagnostic(string what) => new(
        DiagnosticSeverity.Warning, DiagnosticCode.GitCommandFailed, $"Could not {what}: the git command failed or timed out.");

    private enum GitRunStatus
    {
        Completed,
        ExecutableNotFound,
        TimedOut
    }

    private sealed record GitRunResult(GitRunStatus Status, bool Success, string StdOut, string StdErr)
    {
        public static readonly GitRunResult NotFound = new(GitRunStatus.ExecutableNotFound, false, "", "");
        public static readonly GitRunResult TimedOutResult = new(GitRunStatus.TimedOut, false, "", "");
    }

    private static GitRunResult RunGit(
        string workingDirectory, string gitExecutable, int timeoutMilliseconds, params string[] args)
    {
        var startInfo = new ProcessStartInfo(gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Process.Start returned null.");
        }
        catch (Win32Exception)
        {
            return GitRunResult.NotFound;
        }
        catch (FileNotFoundException)
        {
            return GitRunResult.NotFound;
        }

        using (process)
        {
            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            var exited = process.WaitForExit(timeoutMilliseconds);
            if (!exited)
            {
                TryKill(process);
                return GitRunResult.TimedOutResult;
            }

            var stdOut = stdOutTask.Wait(timeoutMilliseconds) ? stdOutTask.Result : string.Empty;
            var stdErr = stdErrTask.Wait(timeoutMilliseconds) ? stdErrTask.Result : string.Empty;

            return new GitRunResult(GitRunStatus.Completed, process.ExitCode == 0, stdOut, stdErr);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the timeout check and the kill attempt - fine.
        }
    }
}
