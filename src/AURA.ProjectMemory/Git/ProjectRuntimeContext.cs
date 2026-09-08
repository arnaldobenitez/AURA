using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Git;

/// <summary>
/// Reusable, UI-agnostic snapshot of everything an agent needs to know to continue work on a
/// project (AURA_PROJECT_MEMORY v1.0 Etapa 6 §32/§53): project identity, local machine profile,
/// optional Git context, and the currently open sessions on this machine. The JSON shape of
/// <c>aura project context</c> maps directly to this type; no logic lives in the CLI.
/// </summary>
public sealed record ProjectRuntimeContext(
    ProjectRuntimeInfo Project,
    MachineValidationResult? Machine,
    GitContext Git,
    IReadOnlyList<SessionState> OpenSessions,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record ProjectRuntimeInfo(
    string? ProjectRoot,
    string? ProjectId,
    string? ProjectName,
    bool IsValid,
    IReadOnlyList<Diagnostic> ValidationDiagnostics);
