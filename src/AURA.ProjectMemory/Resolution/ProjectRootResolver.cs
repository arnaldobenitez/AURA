using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Schemas;

namespace AURA.ProjectMemory.Resolution;

/// <summary>
/// Implements the PROJECT_ROOT detection algorithm from AURA_PROJECT_MEMORY v1.0 §6 and its
/// Etapa 2/3 clarifications (docs/aura/project-memory/README.md, "Contrato operativo de
/// PROJECT_ROOT"): walk up from the start path; the first directory containing a valid
/// <c>.aura/project.json</c> is PROJECT_ROOT. A manifest that exists but fails schema
/// validation stops the search at that level with an error - it is never skipped in favor of
/// an ancestor, so a broken nested project can never be silently bypassed.
/// </summary>
public static class ProjectRootResolver
{
    /// <param name="startPath">Directory to start searching from.</param>
    /// <param name="workspaceBoundary">
    /// Optional inclusive upper bound: the resolver never inspects an ancestor above this
    /// directory. When null, the search stops only at the filesystem root.
    /// </param>
    public static ProjectRootResolution Resolve(string startPath, string? workspaceBoundary = null)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        var boundary = workspaceBoundary is null ? null : Path.GetFullPath(workspaceBoundary);

        while (true)
        {
            var manifestPath = Path.Combine(current.FullName, ".aura", "project.json");

            if (File.Exists(manifestPath))
            {
                return ResolveAtManifest(current.FullName, manifestPath);
            }

            var isAtBoundary = boundary is not null &&
                string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar),
                               boundary.TrimEnd(Path.DirectorySeparatorChar),
                               StringComparison.OrdinalIgnoreCase);

            if (isAtBoundary || current.Parent is null)
            {
                return ProjectRootResolution.NotFound([
                    new Diagnostic(
                        DiagnosticSeverity.Error,
                        DiagnosticCode.ProjectRootNotFound,
                        "No .aura/project.json was found in the starting directory or any ancestor " +
                        "up to the search boundary.",
                        startPath)
                ]);
            }

            current = current.Parent;
        }
    }

    private static ProjectRootResolution ResolveAtManifest(string projectRoot, string manifestPath)
    {
        string rawJson;
        try
        {
            rawJson = File.ReadAllText(manifestPath);
        }
        catch (IOException ex)
        {
            return ProjectRootResolution.NotFound([
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.InvalidProjectManifest,
                    $"Could not read '.aura/project.json': {ex.Message}",
                    manifestPath)
            ]);
        }

        System.Text.Json.Nodes.JsonNode? node;
        try
        {
            node = ProjectManifestSerializer.ParseToNode(rawJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ProjectRootResolution.NotFound([
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.InvalidProjectManifest,
                    $"'.aura/project.json' is not valid JSON: {ex.Message}",
                    manifestPath)
            ]);
        }

        if (node is null)
        {
            return ProjectRootResolution.NotFound([
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.InvalidProjectManifest,
                    "'.aura/project.json' is empty.",
                    manifestPath)
            ]);
        }

        var element = System.Text.Json.JsonSerializer.SerializeToElement(node);
        var evaluation = ManifestSchemaProvider.ProjectSchema.Evaluate(element,
            new Json.Schema.EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.List });

        if (!evaluation.IsValid)
        {
            var diagnostics = CollectSchemaErrors(evaluation, manifestPath).ToList();
            return ProjectRootResolution.NotFound(diagnostics);
        }

        var manifest = ProjectManifestSerializer.FromNode(node);
        if (manifest is null)
        {
            return ProjectRootResolution.NotFound([
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.InvalidProjectManifest,
                    "'.aura/project.json' passed schema validation but could not be projected into a manifest.",
                    manifestPath)
            ]);
        }

        return new ProjectRootResolution(true, projectRoot, manifest, []);
    }

    internal static IEnumerable<Diagnostic> CollectSchemaErrors(
        Json.Schema.EvaluationResults evaluation, string manifestPath)
    {
        foreach (var detail in evaluation.Details ?? [])
        {
            if (detail.Errors is { Count: > 0 } errors)
            {
                foreach (var (_, message) in errors)
                {
                    yield return new Diagnostic(
                        DiagnosticSeverity.Error,
                        DiagnosticCode.InvalidProjectManifest,
                        $"{detail.InstanceLocation}: {message}",
                        manifestPath);
                }
            }
        }
    }
}
