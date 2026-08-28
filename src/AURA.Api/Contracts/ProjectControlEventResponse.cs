using AURA.Application.ProjectControl;

namespace AURA.Api.Contracts;

public sealed record ProjectControlEventResponse(
    string Status,
    long? ItemId,
    long? CurrentLastItemId,
    LeaseInfoDto? Lease = null);

public sealed record ProjectControlValidationProblemResponse(string Status, IReadOnlyList<string> Errors);
