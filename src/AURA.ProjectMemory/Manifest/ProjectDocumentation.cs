namespace AURA.ProjectMemory.Manifest;

/// <summary>
/// The <c>documentation</c> map of <c>.aura/project.json</c>. All paths are relative to
/// PROJECT_ROOT and use '/' as separator, per AURA_PROJECT_MEMORY v1.0 §6/§8.2.
/// <see cref="Extra"/> carries any additional declared module path (e.g. "decisions",
/// "backlog", "architecture") without hardcoding the optional-module vocabulary here.
/// </summary>
public sealed record ProjectDocumentation(
    string Project,
    string CurrentState,
    string History,
    IReadOnlyDictionary<string, string> Extra);
