using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Application.ProjectControl;

namespace AURA.IntegrationTests.ProjectControl;

[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class CreateProjectEndpointTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    private Task<HttpResponseMessage> PostCreateProjectRawAsync(
        int tenantId,
        string code,
        string name,
        string? description = null,
        string? workingPath = null,
        string? repository = null,
        string? status = null,
        int? humanResponsibleId = null)
    {
        var request = new CreateProjectRequest(tenantId, code, name, description, workingPath, repository, status, humanResponsibleId);
        return fixture.Client.PostAsJsonAsync("/api/project-control/projects", request, JsonOptions);
    }

    [SkippableFact]
    public async Task CreateProject_NewCode_ReturnsCreatedAndAppearsOnBoardImmediately()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        var response = await PostCreateProjectRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "arterio", "Arterio");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CREATED", body.GetProperty("status").GetString());
        var projectId = body.GetProperty("projectId").GetInt32();
        Assert.True(projectId > 0);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        var project = Assert.Single(board!.Projects, p => p.ProjectId == projectId);
        Assert.Equal("ARTERIO", project.ProjectCode);
        Assert.Equal("Arterio", project.ProjectName);
        Assert.Equal("ACTIVE", project.ProjectStatus);
    }

    [SkippableFact]
    public async Task CreateProject_RetriedWithSameCodeAndName_IsIdempotent()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        var first = await PostCreateProjectRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "IDEMPROJ", "Proyecto Idempotente");
        var second = await PostCreateProjectRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "IDEMPROJ", "Proyecto Idempotente");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ALREADY_EXISTS", secondBody.GetProperty("status").GetString());
        Assert.Equal(firstBody.GetProperty("projectId").GetInt32(), secondBody.GetProperty("projectId").GetInt32());

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        Assert.Single(board!.Projects, p => p.ProjectCode == "IDEMPROJ");
    }

    [SkippableFact]
    public async Task CreateProject_SameCodeDifferentName_ReturnsConflict()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        await PostCreateProjectRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CONFLICTPRJ", "Nombre Original");
        var response = await PostCreateProjectRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CONFLICTPRJ", "Nombre Distinto");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CONFLICT", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task CreateProject_MissingRequiredFields_ReturnsValidationFailed()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        var response = await PostCreateProjectRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("status").GetString());
        var errors = body.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(errors, e => e!.Contains("projectCode"));
        Assert.Contains(errors, e => e!.Contains("projectName"));
    }

    [SkippableFact]
    public async Task CreateProject_CodeTooLong_ReturnsValidationFailed()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        var response = await PostCreateProjectRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, new string('A', 51), "Codigo largo");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task CreateProject_WithHumanResponsible_PersistsAndReadsBackOnBoard()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 701, "Arnaldo Benitez");

        var response = await PostCreateProjectRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "RESPPROJ", "Proyecto con responsable", humanResponsibleId: 701);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "RESPPROJ");
        Assert.Equal(701, project.HumanResponsibleId);
        Assert.Equal("Arnaldo Benitez", project.HumanResponsibleName);
    }

    [SkippableFact]
    public async Task CreateProject_ConcurrentRequestsSameNewCode_OnlyOneIsCreated()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        const int attempts = 10;
        var tasks = Enumerable.Range(0, attempts)
            .Select(_ => PostCreateProjectRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "RACEPROJ", "Proyecto en carrera"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var alreadyExistsCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);

        Assert.Equal(1, createdCount);
        Assert.Equal(attempts - 1, alreadyExistsCount);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        Assert.Single(board!.Projects, p => p.ProjectCode == "RACEPROJ");
    }
}
