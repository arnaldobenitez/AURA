namespace AURA.Application.ProjectControl;

public enum RegisterProjectEventStatus
{
    Created,
    AlreadyExists,
    ProjectNotFound,
    Conflict,
    ValidationFailed,
}

public sealed record RegisterProjectEventResult(
    RegisterProjectEventStatus Status,
    long? ItemId,
    long? CurrentLastItemId,
    IReadOnlyList<string>? ValidationErrors);
