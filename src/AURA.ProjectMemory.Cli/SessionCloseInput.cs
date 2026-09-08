using System.Text.Json;
using System.Text.Json.Serialization;
using AURA.ProjectMemory.Session;

namespace AURA.ProjectMemory.Cli;

/// <summary>
/// Raw JSON shape accepted by <c>aura project session close --input</c>/<c>--stdin</c>. This is
/// CLI-level I/O plumbing only; <see cref="ToRequest"/> projects it into the reusable
/// <see cref="SessionCloseRequest"/> that <c>AURA.ProjectMemory</c> actually consumes.
/// </summary>
internal sealed record SessionCloseInput(
    string? Result,
    string? Pending,
    string? NextRecommendedStep,
    string? WorkPerformed,
    IReadOnlyList<string>? ModifiedFiles,
    string? Decisions,
    string? Tests,
    string? Problems,
    string? TechnicalDebt,
    string? OsUser,
    string? LocalProjectPath,
    string? GitBranch,
    string? InitialCommit,
    string? FinalCommit,
    string? WorkingTreeInitial,
    string? WorkingTreeFinal,
    IReadOnlyList<string>? UpdatedDocuments)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static SessionCloseInput? Parse(string json) => JsonSerializer.Deserialize<SessionCloseInput>(json, Options);

    public SessionCloseRequest ToRequest() => new(
        Result ?? string.Empty,
        Pending ?? string.Empty,
        NextRecommendedStep ?? string.Empty,
        WorkPerformed,
        ModifiedFiles,
        Decisions,
        Tests,
        Problems,
        TechnicalDebt,
        OsUser,
        LocalProjectPath,
        GitBranch,
        InitialCommit,
        FinalCommit,
        WorkingTreeInitial,
        WorkingTreeFinal,
        UpdatedDocuments);
}
