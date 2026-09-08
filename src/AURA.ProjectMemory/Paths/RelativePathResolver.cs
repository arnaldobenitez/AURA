namespace AURA.ProjectMemory.Paths;

/// <summary>
/// Shared defense-in-depth path safety used by validation, session history writing, and any
/// other component that turns a manifest-declared relative path into a real filesystem path.
/// This sits on top of (not instead of) the project.schema.json relative-path pattern: the
/// schema rejects most unsafe strings syntactically, this re-derives the absolute path and
/// confirms it still resolves inside PROJECT_ROOT before any file I/O is attempted.
/// </summary>
public static class RelativePathResolver
{
    public static bool TryResolveSafePath(string projectRoot, string relativePath, out string absolutePath)
    {
        var normalizedRoot = Path.GetFullPath(projectRoot);
        var combined = Path.Combine(normalizedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        absolutePath = Path.GetFullPath(combined);

        return absolutePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Converts an absolute path known to be inside <paramref name="projectRoot"/> back to a
    /// portable, forward-slash, PROJECT_ROOT-relative path.</summary>
    public static string ToPortableRelativePath(string projectRoot, string absolutePath)
    {
        var relative = Path.GetRelativePath(projectRoot, absolutePath);
        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
    }
}
