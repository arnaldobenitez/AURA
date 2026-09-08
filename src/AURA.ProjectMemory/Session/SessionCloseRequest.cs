namespace AURA.ProjectMemory.Session;

/// <summary>
/// Structured close payload (AURA_PROJECT_MEMORY v1.0 §17/§18). <see cref="Result"/>,
/// <see cref="Pending"/> and <see cref="NextRecommendedStep"/> are mandatory; everything else is
/// omitted from the rendered record when absent - never auto-filled with "N/A"/"unknown".
/// Git fields are optional and user-supplied; this library never runs Git itself (Etapa 6).
/// </summary>
public sealed record SessionCloseRequest(
    string Result,
    string Pending,
    string NextRecommendedStep,
    string? WorkPerformed = null,
    IReadOnlyList<string>? ModifiedFiles = null,
    string? Decisions = null,
    string? Tests = null,
    string? Problems = null,
    string? TechnicalDebt = null,
    string? OsUser = null,
    string? LocalProjectPath = null,
    string? GitBranch = null,
    string? InitialCommit = null,
    string? FinalCommit = null,
    string? WorkingTreeInitial = null,
    string? WorkingTreeFinal = null,
    IReadOnlyList<string>? UpdatedDocuments = null);
