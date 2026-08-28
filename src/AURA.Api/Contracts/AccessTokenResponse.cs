namespace AURA.Api.Contracts;

public sealed record AccessTokenResponse(string AccessToken, DateTime ExpiresAtUtc);

public sealed record CreateAgentCredentialResponse(int? CredentialId, string? Secret, IReadOnlyList<string>? Errors);

public sealed record LoginRequestBody(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string UserName);
