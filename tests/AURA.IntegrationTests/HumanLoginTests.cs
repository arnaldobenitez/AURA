using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.IntegrationTests.ProjectControl;

namespace AURA.IntegrationTests;

/// <summary>
/// Basic web login (2026-08-28): email+password against the real tenant-1 Usuarios seed, and
/// the fact that GET /api/project-control now requires a valid token too. Read-only against
/// tenant 1 (login is just a SELECT + JWT issuance) - never touches or resets tenant-1 project
/// data, unlike the ProjectControlDatabaseFixture-based tests, which stay on tenants 900001/900002.
/// </summary>
[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class HumanLoginTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    [SkippableFact]
    public async Task Login_DefaultSeededOwner_ReturnsTokenAndUserName()
    {
        RequireDatabase(fixture);

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/auth/login", new { email = "arnaldo@productorbenitez.com", password = "Cataratas3" }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Arnaldo Benitez", body.GetProperty("userName").GetString());
    }

    [SkippableFact]
    public async Task Login_CaseInsensitiveEmail_Succeeds()
    {
        RequireDatabase(fixture);

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/auth/login", new { email = "ARNALDO@Productorbenitez.com", password = "Cataratas3" }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        RequireDatabase(fixture);

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/auth/login", new { email = "arnaldo@productorbenitez.com", password = "not-the-password" }, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        RequireDatabase(fixture);

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/auth/login", new { email = "nadie@nowhere.invalid", password = "whatever" }, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetBoard_WithoutAnyToken_ReturnsUnauthorized()
    {
        RequireDatabase(fixture);

        // A fresh client with no default-bearer handler, unlike fixture.Client - proves the
        // dashboard genuinely cannot be viewed without a valid login.
        using var anonymousClient = fixture.Factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/project-control?tenantId=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetBoard_WithLoginToken_Succeeds()
    {
        RequireDatabase(fixture);

        var loginResponse = await fixture.Client.PostAsJsonAsync(
            "/api/auth/login", new { email = "arnaldo@productorbenitez.com", password = "Cataratas3" }, JsonOptions);
        var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = login.GetProperty("accessToken").GetString();

        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/project-control?tenantId=1");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
