using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Resolution;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Git;

/// <summary>
/// Gathers the unified runtime context of a project without modifying anything (AURA_PROJECT_MEMORY
/// v1.0 Etapa 6 §31/§32). Combines existing Core validation, local machine profile validation,
/// Git context, and the set of currently open sessions.
/// </summary>
public static class ProjectContextService
{
    public static ProjectRuntimeContext Get(string startPath, string? machineProfilePath = null, string? runtimeRoot = null)
    {
        var diagnostics = new List<Diagnostic>();

        var resolution = ProjectRootResolver.Resolve(startPath);
        if (!resolution.Found || resolution.Manifest is null || resolution.ProjectRoot is null)
        {
            diagnostics.AddRange(resolution.Diagnostics);
            return new ProjectRuntimeContext(
                new ProjectRuntimeInfo(null, null, null, false, diagnostics.ToList()),
                null,
                GitContext.Unavailable(),
                [],
                diagnostics);
        }

        var validation = ProjectMemoryValidator.Validate(startPath, machineProfilePath: machineProfilePath);
        var projectInfo = new ProjectRuntimeInfo(
            resolution.ProjectRoot,
            resolution.Manifest.ProjectId,
            resolution.Manifest.ProjectName,
            validation.IsValid,
            validation.Diagnostics);

        diagnostics.AddRange(validation.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));

        var machine = MachineProfileValidator.Validate(machineProfilePath);
        // Missing machine profile is not noisy at context level - it's just context.
        diagnostics.AddRange(machine.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));

        var git = GitContextProvider.Capture(resolution.ProjectRoot);
        // Git absence / no repo is normal; only forward warnings or worse (merge conflicts, failures).
        diagnostics.AddRange(git.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));

        var openSessions = SessionStateStore.ListActive(resolution.Manifest.ProjectId, runtimeRoot);

        return new ProjectRuntimeContext(projectInfo, machine, git, openSessions, diagnostics);
    }
}
