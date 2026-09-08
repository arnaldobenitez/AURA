using System.Diagnostics;

namespace AURA.ProjectMemory.Tests.TestSupport;

/// <summary>Launches the real, built <c>aura</c> CLI as a subprocess for end-to-end tests.</summary>
internal static class CliProcess
{
    private static readonly string DllPath = LocateCliDll();

    public static (int ExitCode, string StdOut, string StdErr) Run(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(DllPath);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdOut, stdErr);
    }

    private static string LocateCliDll()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var relativeToBin = Path.GetRelativePath(
            Path.Combine(repoRoot, "tests", "AURA.ProjectMemory.Tests", "bin"), AppContext.BaseDirectory);

        var candidate = Path.Combine(repoRoot, "src", "AURA.ProjectMemory.Cli", "bin", relativeToBin, "aura.dll");
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                $"aura.dll was not found at '{candidate}'. Build AURA.ProjectMemory.Cli first.", candidate);
        }

        return candidate;
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AURA.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate AURA.sln.");
    }
}
