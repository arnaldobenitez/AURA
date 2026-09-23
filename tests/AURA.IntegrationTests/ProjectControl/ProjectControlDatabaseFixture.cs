using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Api.Admin;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AURA.IntegrationTests.ProjectControl;

/// <summary>
/// These scenarios (last-item resolution, idempotency, locking, tenant isolation) live in
/// raw SQL against the legacy tables, so they're only meaningfully verifiable against a
/// real SQL Server - the same dev database already used by scripts/database-verify.ps1.
/// Two clearly out-of-range tenant ids are used so this never touches real tenant data.
///
/// AURA_PROTOCOL v2.1 (AURA-PROTOCOL-V2-01) requires every write to be authenticated, so this
/// fixture also provisions one AgentCredential per (tenant, role) via the admin endpoint and
/// mints/caches its JWT - tests attach the token matching the actor.type they exercise via
/// GetBearerToken. See docs/aura/AURA_PROTOCOL_v2.1.md.
/// </summary>
public sealed class ProjectControlDatabaseFixture : IAsyncLifetime
{
    public const int PrimaryTenantId = 900001;
    public const int SecondaryTenantId = 900002;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string? _connectionString;
    private readonly Dictionary<(int TenantId, string ActorType), string> _tokens = new();
    private readonly Dictionary<(int TenantId, string ActorType), int> _credentialIds = new();
    private HttpClient _bootstrapClient = null!;

    public WebApplicationFactory<global::Program> Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public bool IsAvailable => _connectionString is not null;

    public async Task InitializeAsync()
    {
        _connectionString = ProjectControlTestSupport.TryGetConnectionString();

        if (_connectionString is null)
        {
            return;
        }

        var connectionString = _connectionString;
        Factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                });
            });
        });
        // Raw client (no default auth) - used only for the bootstrap calls below (admin/token
        // endpoints, which are anonymous or use X-Admin-Api-Key, never a Bearer token).
        _bootstrapClient = Factory.CreateClient();

        await ResetTestDataAsync();
        await ProvisionCredentialsAsync(PrimaryTenantId);
        await ProvisionCredentialsAsync(SecondaryTenantId);

        // Public Client: since GET /api/project-control and its history are now [Authorize]-gated
        // too (basic web login, see the human-login feature), every test's read call needs a
        // valid Bearer token. Rather than touching every GetFromJsonAsync call site, this client
        // auto-attaches a default one (a real, already-provisioned CODER token) to any outgoing
        // request that doesn't already carry its own Authorization header - writes that call
        // AddBearer(...) explicitly are unaffected, since the handler only fills in what's absent.
        Client = Factory.CreateDefaultClient(new DefaultBearerHandler(() => GetBearerToken(PrimaryTenantId, "CODER")));
    }

    private sealed class DefaultBearerHandler(Func<string> getDefaultToken) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer", getDefaultToken());
            return base.SendAsync(request, cancellationToken);
        }
    }

    private async Task ProvisionCredentialsAsync(int tenantId)
    {
        foreach (var actorType in new[] { "CODER", "AI_ANALYST", "HUMAN" })
        {
            var (credentialId, secret) = await CreateCredentialAsync(tenantId, actorType, $"test-{actorType}");
            _credentialIds[(tenantId, actorType)] = credentialId;
            _tokens[(tenantId, actorType)] = await MintTokenAsync(tenantId, credentialId, secret);
        }
    }

    /// <summary>Creates a new AgentCredential via the admin endpoint; returns (credentialId, plaintext secret).</summary>
    public async Task<(int CredentialId, string Secret)> CreateCredentialAsync(int tenantId, string actorType, string agentName)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/agent-credentials")
        {
            Content = JsonContent.Create(new { tenantId, actorType, agentName }, options: JsonOptions),
        };
        createRequest.Headers.Add("X-Admin-Api-Key", AdminApiKeyFilter.DevelopmentOnlyPlaceholder);
        var createResponse = await _bootstrapClient.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (created.GetProperty("credentialId").GetInt32(), created.GetProperty("secret").GetString()!);
    }

    /// <summary>Exchanges a credential id/secret for a JWT access token via POST /api/auth/token.</summary>
    public async Task<string> MintTokenAsync(int tenantId, int credentialId, string secret)
    {
        var tokenResponse = await _bootstrapClient.PostAsJsonAsync("/api/auth/token", new { tenantId, credentialId, secret }, JsonOptions);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return token.GetProperty("accessToken").GetString()!;
    }

    /// <summary>Raw (possibly-failing) token exchange, for negative-path tests.</summary>
    public Task<HttpResponseMessage> RequestTokenRawAsync(int tenantId, int credentialId, string secret) =>
        _bootstrapClient.PostAsJsonAsync("/api/auth/token", new { tenantId, credentialId, secret }, JsonOptions);

    public async Task RevokeCredentialAsync(int tenantId, int credentialId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/agent-credentials/{credentialId}/revoke?tenantId={tenantId}");
        request.Headers.Add("X-Admin-Api-Key", AdminApiKeyFilter.DevelopmentOnlyPlaceholder);
        var response = await _bootstrapClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Bearer token for a shared, pre-provisioned credential of the given role/tenant.</summary>
    public string GetBearerToken(int tenantId, string actorType) => _tokens[(tenantId, actorType)];

    public int GetCredentialId(int tenantId, string actorType) => _credentialIds[(tenantId, actorType)];

    public void AddBearer(HttpRequestMessage request, int tenantId, string actorType) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetBearerToken(tenantId, actorType));

    public void AddBearer(HttpRequestMessage request, string accessToken) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    /// <summary>
    /// Forces the currently active lease for (project, workStream) into the past, to test
    /// expiry without waiting out a real AgentLease:DurationMinutes window.
    /// </summary>
    public async Task ExpireLeaseAsync(int tenantId, int projectId, string? workStream = null)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE li
            SET LeaseExpiresAtUtc = DATEADD(MINUTE, -5, SYSUTCDATETIME())
            FROM dbo.ProProyectosItems li
            WHERE li.idTenant = @tenantId AND li.IdProyecto = @projectId
                AND (li.sWorkStream = @workStream OR (li.sWorkStream IS NULL AND @workStream IS NULL))
                AND li.IdItems = (
                    SELECT MAX(IdItems) FROM dbo.ProProyectosItems
                    WHERE idTenant = @tenantId AND IdProyecto = @projectId
                        AND (sWorkStream = @workStream OR (sWorkStream IS NULL AND @workStream IS NULL))
                );
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.Add("@workStream", System.Data.SqlDbType.NVarChar).Value = (object?)workStream ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString is null)
        {
            return;
        }

        await ResetTestDataAsync();
        await DeleteTestCredentialsAsync();
        Client.Dispose();
        _bootstrapClient.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>
    /// AgentCredential rows are deliberately NOT touched by ResetTestDataAsync (many tests call
    /// it mid-run, and that would invalidate the tokens cached in _tokens); cleaned up once here
    /// instead, at the very end of the fixture's life.
    /// </summary>
    private async Task DeleteTestCredentialsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AgentCredential WHERE idTenant IN (@t1, @t2);";
        command.Parameters.AddWithValue("@t1", PrimaryTenantId);
        command.Parameters.AddWithValue("@t2", SecondaryTenantId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetTestDataAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SET QUOTED_IDENTIFIER ON;
            DELETE FROM ProProyectoRoadmapRevision WHERE idTenant IN (@t1, @t2);
            DELETE FROM ProProyectosItems WHERE idTenant IN (@t1, @t2);
            DELETE FROM ProProyectoEtapa WHERE idTenant IN (@t1, @t2);
            DELETE FROM ProProyectoEXEC WHERE idTenant IN (@t1, @t2);
            DELETE FROM ProProyectosCabecera WHERE idTenant IN (@t1, @t2);
            DELETE FROM Usuarios WHERE IdTenant IN (@t1, @t2);
            DELETE FROM ProCodificadores WHERE IdTenant IN (@t1, @t2);
            """;
        command.Parameters.AddWithValue("@t1", PrimaryTenantId);
        command.Parameters.AddWithValue("@t2", SecondaryTenantId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Inserts a legacy ProCodificadores catalog row directly - used to reproduce rows whose
    /// sNombre actually describes an environment (e.g. "Vs Code"), not a person.
    /// </summary>
    public async Task CreateCodificadorAsync(int tenantId, int idCodificador, string name)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO ProCodificadores (IdTenant, IdCodificador, sNombre) VALUES (@tenantId, @id, @name);";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@id", idCodificador);
        command.Parameters.AddWithValue("@name", name);
        await command.ExecuteNonQueryAsync();
    }

    public async Task CreateUsuarioAsync(int tenantId, int idUsuario, string name)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Usuarios (IdTenant, IdUsuario, sNombre) VALUES (@tenantId, @idUsuario, @name);";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@idUsuario", idUsuario);
        command.Parameters.AddWithValue("@name", name);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Inserts an item row directly with raw SQL, bypassing the API - used to reproduce
    /// historical rows written before sModelo (and other event columns) existed.
    /// </summary>
    public async Task<long> CreateLegacyItemWithoutModelAsync(
        int tenantId, int projectId, int actorType, int coderId, string summary)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ProProyectosItems
                (idTenant, IdProyecto, IdItems, dFechaEntrada, nCodificador, nActorTipo, nEstadoTrabajo,
                 IdEvento, nTipoEvento, sResumen)
            OUTPUT INSERTED.IdItems
            SELECT
                @tenantId, @projectId,
                ISNULL((SELECT MAX(IdItems) FROM ProProyectosItems WHERE idTenant = @tenantId AND IdProyecto = @projectId), 0) + 1,
                SYSUTCDATETIME(), @coderId, @actorType, 1,
                @eventId, 2, @summary;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@coderId", coderId);
        command.Parameters.AddWithValue("@actorType", actorType);
        command.Parameters.AddWithValue("@eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("@summary", summary);

        var result = await command.ExecuteScalarAsync();
        return (long)result!;
    }

    public async Task SetItemProgressAsync(
        int tenantId,
        int projectId,
        long itemId,
        string execDescription,
        string stage,
        decimal stageProgress,
        decimal execProgress)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ProProyectosItems
            SET sDescripcion = @execDescription,
                sEtapa = @stage,
                nPorcEtapa = @stageProgress,
                nPorcEXEC = @execProgress
            WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdItems = @itemId;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@itemId", itemId);
        command.Parameters.AddWithValue("@execDescription", execDescription);
        command.Parameters.AddWithValue("@stage", stage);
        command.Parameters.AddWithValue("@stageProgress", stageProgress);
        command.Parameters.AddWithValue("@execProgress", execProgress);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CreateProjectAsync(int tenantId, string code, string name, byte status = 1)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ProProyectosCabecera
                (idTenant, IdProyecto, sNombre, sDescripcion, dFecCreacion, sPathTrabajo, nEstado, sCodigo, sRepositorio)
            OUTPUT INSERTED.IdProyecto
            SELECT
                @tenantId,
                ISNULL((SELECT MAX(IdProyecto) FROM ProProyectosCabecera WHERE idTenant = @tenantId), 0) + 1,
                @name, NULL, CAST(SYSUTCDATETIME() AS date), NULL, @status, @code, NULL;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@status", status);

        var result = await command.ExecuteScalarAsync();
        return (int)result!;
    }
}
