using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AURA.IntegrationTests.ProjectControl;

/// <summary>
/// These scenarios (last-item resolution, idempotency, locking, tenant isolation) live in
/// raw SQL against the legacy tables, so they're only meaningfully verifiable against a
/// real SQL Server - the same dev database already used by scripts/database-verify.ps1.
/// Two clearly out-of-range tenant ids are used so this never touches real tenant data.
/// </summary>
public sealed class ProjectControlDatabaseFixture : IAsyncLifetime
{
    public const int PrimaryTenantId = 900001;
    public const int SecondaryTenantId = 900002;

    private string? _connectionString;

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
        Client = Factory.CreateClient();

        await ResetTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString is null)
        {
            return;
        }

        await ResetTestDataAsync();
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    public async Task ResetTestDataAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SET QUOTED_IDENTIFIER ON;
            DELETE FROM ProProyectosItems WHERE idTenant IN (@t1, @t2);
            DELETE FROM ProProyectosCabecera WHERE idTenant IN (@t1, @t2);
            DELETE FROM Usuarios WHERE IdTenant IN (@t1, @t2);
            """;
        command.Parameters.AddWithValue("@t1", PrimaryTenantId);
        command.Parameters.AddWithValue("@t2", SecondaryTenantId);
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
