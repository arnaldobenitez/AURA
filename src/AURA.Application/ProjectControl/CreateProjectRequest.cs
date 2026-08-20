namespace AURA.Application.ProjectControl;

public sealed record CreateProjectRequest(
    int TenantId,
    string ProjectCode,
    string ProjectName,
    string? Description,
    string? WorkingPath,
    string? Repository,
    string? Status,
    int? HumanResponsibleId);
