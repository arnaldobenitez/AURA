namespace AURA.Api.Contracts;

public sealed record ProjectControlEventResponse(string Status, long? ItemId, long? CurrentLastItemId);

public sealed record ProjectControlValidationProblemResponse(string Status, IReadOnlyList<string> Errors);
