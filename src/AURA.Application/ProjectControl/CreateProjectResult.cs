namespace AURA.Application.ProjectControl;

public enum CreateProjectResultStatus
{
    Created,
    AlreadyExists,
    Conflict,
    ValidationFailed,
}

public sealed record CreateProjectResult(
    CreateProjectResultStatus Status,
    int? ProjectId,
    IReadOnlyList<string>? ValidationErrors);
