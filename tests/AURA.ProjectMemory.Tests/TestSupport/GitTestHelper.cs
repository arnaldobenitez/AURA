using System.Diagnostics;

namespace AURA.ProjectMemory.Tests.TestSupport;

/// <summary>
/// Utility to create real, isolated Git repositories inside temp directories for tests. Configures
/// user.name and user.email inside the temp repo only - never globally. No other test or
/// production code should use this helper.
/// </summary>
internal static class GitTestHelper
{
    public static void InitRepository(string repoPath)
    {
        RunGit(repoPath, "init");
        RunGit(repoPath, "config", "user.name", "AURA Tests");
        RunGit(repoPath, "config", "user.email", "aura-tests@invalid.local");
    }

    public static string CommitFile(string repoPath, string fileName, string content, string message)
    {
        var fullPath = System.IO.Path.Combine(repoPath, fileName);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
        RunGit(repoPath, "add", fileName);
        RunGit(repoPath, "commit", "-m", message);

        var id = GetCommitHash(repoPath);
        return id;
    }

    public static string GetCommitHash(string repoPath) => RunGit(repoPath, "rev-parse", "HEAD");

    public static void ModifyFile(string repoPath, string fileName, string content) =>
        File.WriteAllText(System.IO.Path.Combine(repoPath, fileName), content);

    public static string GetBranch(string repoPath) => RunGit(repoPath, "rev-parse", "--abbrev-ref", "HEAD");

    public static string RunGit(string repoPath, params string[] args)
    {
        var (exitCode, stdout, stderr) = RunGitRaw(repoPath, args);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(" ", args)} failed: {stderr}");
        }

        return stdout.Trim();
    }

    /// <summary>Runs git and returns the exit code without throwing. Useful for merge conflicts.</summary>
    public static (int ExitCode, string StdOut, string StdErr) RunGitRaw(string repoPath, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout.Trim(), stderr.Trim());
    }
}
