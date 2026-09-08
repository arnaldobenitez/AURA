using System.Text.Json;
using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Manager;

public static class ProjectManagerIndexStore
{
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aura", "projects", "index.json");

    public static ProjectManagerIndex Load(string? indexPath = null)
    {
        var path = indexPath ?? DefaultPath();
        if (!File.Exists(path))
        {
            return new ProjectManagerIndex([]);
        }

        try
        {
            var index = JsonSerializer.Deserialize<ProjectManagerIndex>(File.ReadAllText(path), Options);
            return index ?? new ProjectManagerIndex([]);
        }
        catch (JsonException)
        {
            return new ProjectManagerIndex([]);
        }
        catch (IOException)
        {
            return new ProjectManagerIndex([]);
        }
    }

    public static void Save(ProjectManagerIndex index, string? indexPath = null)
    {
        var path = indexPath ?? DefaultPath();
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(index, Options));
    }

    public static Diagnostic AliasConflict(string alias) => new(
        DiagnosticSeverity.Error,
        "PM_ALIAS_CONFLICT",
        $"Alias '{alias}' is already assigned to another remembered project.",
        "index.json");

    public static Diagnostic ProjectNotFound(string selector) => new(
        DiagnosticSeverity.Error,
        "PM_PROJECT_NOT_FOUND",
        $"No remembered project matched '{selector}'.",
        "index.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
