using AURA.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AURA.Infrastructure.ProjectControl;

/// <summary>
/// Applies additive-only schema changes to the legacy ProProyectos* tables and seeds the
/// tenant 1 project catalog. Never runs automatically; must be triggered explicitly
/// (see scripts/project-control-schema-update.ps1) to stay consistent with the "no
/// automatic migration on startup" rule already used for AuraDbContext.
/// </summary>
public sealed class ProjectControlSchemaMigrator(
    AuraDbContext dbContext,
    ILogger<ProjectControlSchemaMigrator> logger) : IProjectControlSchemaMigrator
{
    private static readonly (string Code, string Name, string? Path)[] SeedProjects =
    [
        ("AURA", "AURA", @"D:\Desarrollos\AURA"),
        ("SINITRAX", "SiniTrax", null),
        ("CONTADORO", "Contadoro", null),
        ("TURNOMEDIX", "TurnoMedix", null),
        ("MANDADINI", "Mandadini", null),
        ("POLICURSOS", "Policursos", null),
    ];

    private const int TenantId = 1;

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();

            await ApplySchemaAsync(connection, sqlTransaction, cancellationToken);
            await SeedProjectsAsync(connection, sqlTransaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Project control schema and seed applied successfully.");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task ApplySchemaAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            // ProProyectosCabecera additive columns
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosCabecera') AND name = 'sCodigo')
                ALTER TABLE dbo.ProProyectosCabecera ADD sCodigo varchar(50) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosCabecera') AND name = 'sRepositorio')
                ALTER TABLE dbo.ProProyectosCabecera ADD sRepositorio nvarchar(1000) NULL;
            """,

            // ProProyectosItems additive columns
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'IdEvento')
                ALTER TABLE dbo.ProProyectosItems ADD IdEvento uniqueidentifier NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'IdItemAnterior')
                ALTER TABLE dbo.ProProyectosItems ADD IdItemAnterior bigint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nTipoEvento')
                ALTER TABLE dbo.ProProyectosItems ADD nTipoEvento tinyint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nActorTipo')
                ALTER TABLE dbo.ProProyectosItems ADD nActorTipo tinyint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nEstadoTrabajo')
                ALTER TABLE dbo.ProProyectosItems ADD nEstadoTrabajo tinyint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nControlTipo')
                ALTER TABLE dbo.ProProyectosItems ADD nControlTipo tinyint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nControlId')
                ALTER TABLE dbo.ProProyectosItems ADD nControlId int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sResumen')
                ALTER TABLE dbo.ProProyectosItems ADD sResumen nvarchar(300) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sProximaAccion')
                ALTER TABLE dbo.ProProyectosItems ADD sProximaAccion nvarchar(1000) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sBloqueo')
                ALTER TABLE dbo.ProProyectosItems ADD sBloqueo nvarchar(1000) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sReferencia')
                ALTER TABLE dbo.ProProyectosItems ADD sReferencia nvarchar(1000) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sModelo')
                ALTER TABLE dbo.ProProyectosItems ADD sModelo nvarchar(100) NULL;
            """,

            // Uniqueness / idempotency indexes (filtered so historical/legacy NULLs never collide)
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProProyectosCabecera') AND name = 'UX_ProProyectosCabecera_Tenant_Codigo')
                CREATE UNIQUE INDEX UX_ProProyectosCabecera_Tenant_Codigo
                    ON dbo.ProProyectosCabecera (idTenant, sCodigo)
                    WHERE sCodigo IS NOT NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'UX_ProProyectosItems_Tenant_Proyecto_Evento')
                CREATE UNIQUE INDEX UX_ProProyectosItems_Tenant_Proyecto_Evento
                    ON dbo.ProProyectosItems (idTenant, IdProyecto, IdEvento)
                    WHERE IdEvento IS NOT NULL;
            """,
        };

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedProjectsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (var (code, name, path) in SeedProjects)
        {
            await using var existingByCodeCommand = connection.CreateCommand();
            existingByCodeCommand.Transaction = transaction;
            existingByCodeCommand.CommandText =
                "SELECT COUNT(1) FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId AND sCodigo = @code;";
            existingByCodeCommand.Parameters.AddWithValue("@tenantId", TenantId);
            existingByCodeCommand.Parameters.AddWithValue("@code", code);
            var alreadyHasCode = (int)(await existingByCodeCommand.ExecuteScalarAsync(cancellationToken))! > 0;

            if (alreadyHasCode)
            {
                continue;
            }

            await using var existingByNameCommand = connection.CreateCommand();
            existingByNameCommand.Transaction = transaction;
            existingByNameCommand.CommandText =
                "SELECT COUNT(1) FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId AND LOWER(sNombre) = LOWER(@name);";
            existingByNameCommand.Parameters.AddWithValue("@tenantId", TenantId);
            existingByNameCommand.Parameters.AddWithValue("@name", name);
            var existsByName = (int)(await existingByNameCommand.ExecuteScalarAsync(cancellationToken))! > 0;

            if (existsByName)
            {
                await using var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText =
                    "UPDATE dbo.ProProyectosCabecera SET sCodigo = @code " +
                    "WHERE idTenant = @tenantId AND LOWER(sNombre) = LOWER(@name);";
                updateCommand.Parameters.AddWithValue("@code", code);
                updateCommand.Parameters.AddWithValue("@tenantId", TenantId);
                updateCommand.Parameters.AddWithValue("@name", name);
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
                continue;
            }

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO dbo.ProProyectosCabecera
                    (idTenant, IdProyecto, sNombre, sDescripcion, dFecCreacion, sPathTrabajo, nEstado, sCodigo, sRepositorio)
                SELECT
                    @tenantId,
                    ISNULL((SELECT MAX(IdProyecto) FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId), 0) + 1,
                    @name, NULL, CAST(SYSUTCDATETIME() AS date), @path, 1, @code, NULL;
                """;
            insertCommand.Parameters.AddWithValue("@tenantId", TenantId);
            insertCommand.Parameters.AddWithValue("@name", name);
            insertCommand.Parameters.AddWithValue("@path", (object?)path ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@code", code);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
