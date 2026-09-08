using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Integrity;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Paths;
using AURA.ProjectMemory.Resolution;

namespace AURA.ProjectMemory.Validation;

/// <summary>
/// Validates a project against AURA_PROJECT_MEMORY v1.0: resolves PROJECT_ROOT, validates
/// <c>.aura/project.json</c> against its JSON Schema, checks Core document existence, checks
/// documentation targets, and optionally checks the local machine profile. This class never
/// invents a compatibility rule beyond what the normative spec/schemas already state.
/// </summary>
public static class ProjectMemoryValidator
{
    public static ProjectValidationResult Validate(
        string startPath,
        string? workspaceBoundary = null,
        string? machineProfilePath = null)
    {
        var resolution = ProjectRootResolver.Resolve(startPath, workspaceBoundary);

        if (!resolution.Found || resolution.ProjectRoot is null || resolution.Manifest is null)
        {
            return new ProjectValidationResult(false, null, null, resolution.Diagnostics);
        }

        var diagnostics = new List<Diagnostic>();
        var projectRoot = resolution.ProjectRoot;
        var manifest = resolution.Manifest;

        ValidateAuraMd(projectRoot, diagnostics);
        ValidateAgentsMd(projectRoot, diagnostics);
        ValidateCoreDocumentationTargets(projectRoot, manifest, diagnostics);
        ValidateModules(projectRoot, manifest, diagnostics);
        ValidateCurrentStateHasNoConflictMarkers(projectRoot, manifest, diagnostics);
        ValidateHistoryHasNoConflictMarkers(projectRoot, manifest, diagnostics);
        diagnostics.AddRange(MachineProfileValidator.Validate(machineProfilePath).Diagnostics);

        var isValid = diagnostics.All(d => d.Severity != DiagnosticSeverity.Error);

        return new ProjectValidationResult(isValid, projectRoot, manifest.ProjectId, diagnostics);
    }

    private static void ValidateAuraMd(string projectRoot, List<Diagnostic> diagnostics)
    {
        var auraMdPath = Path.Combine(projectRoot, "AURA.md");
        if (!File.Exists(auraMdPath))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.MissingCoreDocument,
                "AURA.md is missing at PROJECT_ROOT.",
                "AURA.md",
                "Create AURA.md from docs/aura/project-memory/templates/AURA.md."));
        }
    }

    private static void ValidateAgentsMd(string projectRoot, List<Diagnostic> diagnostics)
    {
        var agentsMdPath = Path.Combine(projectRoot, "AGENTS.md");
        if (!File.Exists(agentsMdPath))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                DiagnosticCode.AgentsFileAbsent,
                "AGENTS.md is not present. It is a recommended discovery adapter, not part of the Core.",
                "AGENTS.md"));
        }
    }

    private static void ValidateCoreDocumentationTargets(
        string projectRoot, ProjectManifest manifest, List<Diagnostic> diagnostics)
    {
        ValidateFileTarget(projectRoot, manifest.Documentation.Project, "documentation.project", diagnostics,
            DiagnosticCode.MissingCoreDocument, DiagnosticSeverity.Error);

        ValidateFileTarget(projectRoot, manifest.Documentation.CurrentState, "documentation.currentState",
            diagnostics, DiagnosticCode.MissingCoreDocument, DiagnosticSeverity.Error);

        ValidateHistoryTarget(projectRoot, manifest.Documentation.History, diagnostics);
    }

    private static void ValidateHistoryTarget(string projectRoot, string relativePath, List<Diagnostic> diagnostics)
    {
        if (!RelativePathResolver.TryResolveSafePath(projectRoot, relativePath, out var absolutePath))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.AbsoluteDocumentationPath,
                $"documentation.history ('{relativePath}') escapes PROJECT_ROOT.",
                relativePath));
            return;
        }

        if (Directory.Exists(absolutePath))
        {
            return;
        }

        if (File.Exists(absolutePath))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.HistoryNotDirectory,
                $"documentation.history ('{relativePath}') exists but is not a directory.",
                relativePath));
            return;
        }

        diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Error,
            DiagnosticCode.MissingCoreDocument,
            $"documentation.history ('{relativePath}') does not exist.",
            relativePath,
            "Create the directory; an empty history is valid for a new project."));
    }

    private static void ValidateModules(string projectRoot, ProjectManifest manifest, List<Diagnostic> diagnostics)
    {
        if (manifest.Modules is null)
        {
            return;
        }

        foreach (var module in manifest.Modules)
        {
            var matchingKey = manifest.Documentation.Extra.Keys
                .FirstOrDefault(key => string.Equals(key, module, StringComparison.OrdinalIgnoreCase));

            if (matchingKey is null)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCode.MissingOptionalModule,
                    $"Module '{module}' is declared but has no matching entry in documentation.",
                    module));
                continue;
            }

            var relativePath = manifest.Documentation.Extra[matchingKey];

            if (!RelativePathResolver.TryResolveSafePath(projectRoot, relativePath, out var absolutePath))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.AbsoluteDocumentationPath,
                    $"documentation.{matchingKey} ('{relativePath}') escapes PROJECT_ROOT.",
                    relativePath));
                continue;
            }

            if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCode.MissingOptionalModule,
                    $"Module '{module}' declares '{relativePath}', which does not exist.",
                    relativePath));
            }
        }
    }

    /// <summary>
    /// AURA_PROJECT_MEMORY v1.0 Etapa 6 §21: an unresolved Git merge conflict left inside the
    /// consolidated current-state document means it cannot be trusted as the project's real
    /// state, so this is an ERROR (not a warning) - deliberately more severe than a normal
    /// missing/absent check.
    /// </summary>
    private static void ValidateCurrentStateHasNoConflictMarkers(
        string projectRoot, ProjectManifest manifest, List<Diagnostic> diagnostics)
    {
        if (!RelativePathResolver.TryResolveSafePath(
                projectRoot, manifest.Documentation.CurrentState, out var absolutePath) ||
            !File.Exists(absolutePath))
        {
            return;
        }

        string content;
        try
        {
            content = File.ReadAllText(absolutePath);
        }
        catch (IOException)
        {
            return;
        }

        if (ConflictMarkerDetector.ContainsConflictMarkers(content))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.CurrentStateConflictMarkers,
                $"{manifest.Documentation.CurrentState} contains unresolved Git merge conflict markers " +
                "and cannot be treated as a trustworthy consolidated state.",
                manifest.Documentation.CurrentState,
                "Resolve the merge conflict manually; this is never done automatically."));
        }
    }

    /// <summary>
    /// AURA_PROJECT_MEMORY v1.0 Etapa 6 §22: history is append-only evidence. A historical file
    /// containing conflict markers is flagged, never edited or "repaired" automatically.
    /// </summary>
    private static void ValidateHistoryHasNoConflictMarkers(
        string projectRoot, ProjectManifest manifest, List<Diagnostic> diagnostics)
    {
        if (!RelativePathResolver.TryResolveSafePath(projectRoot, manifest.Documentation.History, out var historyDir) ||
            !Directory.Exists(historyDir))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(historyDir, "*.md"))
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            if (ConflictMarkerDetector.ContainsConflictMarkers(content))
            {
                var relative = RelativePathResolver.ToPortableRelativePath(projectRoot, file);
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.HistoryConflictMarkers,
                    $"{relative} contains unresolved Git merge conflict markers. Historical evidence must " +
                    "be reconciled manually, never edited automatically.",
                    relative));
            }
        }
    }

    private static void ValidateFileTarget(
        string projectRoot,
        string relativePath,
        string fieldName,
        List<Diagnostic> diagnostics,
        string missingCode,
        DiagnosticSeverity missingSeverity)
    {
        if (!RelativePathResolver.TryResolveSafePath(projectRoot, relativePath, out var absolutePath))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.AbsoluteDocumentationPath,
                $"{fieldName} ('{relativePath}') escapes PROJECT_ROOT.",
                relativePath));
            return;
        }

        if (!File.Exists(absolutePath))
        {
            diagnostics.Add(new Diagnostic(
                missingSeverity,
                missingCode,
                $"{fieldName} ('{relativePath}') does not exist.",
                relativePath));
        }
    }
}
