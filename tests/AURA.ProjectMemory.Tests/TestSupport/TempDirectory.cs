namespace AURA.ProjectMemory.Tests.TestSupport;

/// <summary>Creates a unique temporary directory for a test and removes it on disposal.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aura-pm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Combine(params string[] segments) => System.IO.Path.Combine([Path, ..segments]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                DeleteRecursive(Path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; leaving a stray temp dir behind must never fail a test run.
        }
        catch (UnauthorizedAccessException)
        {
            // Git creates read-only files/directories (e.g. .git/objects) on Windows.
        }
    }

    private static void DeleteRecursive(string directory)
    {
        foreach (var subDirectory in Directory.GetDirectories(directory))
        {
            DeleteRecursive(subDirectory);
        }

        foreach (var file in Directory.GetFiles(directory))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        try
        {
            File.SetAttributes(directory, FileAttributes.Normal);
            Directory.Delete(directory, recursive: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
