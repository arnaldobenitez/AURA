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
        ("ARTERIO", "Arterio", @"d:\Desarrollos\SmartPhones\Arterio"),
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
            await SeedDefaultHumanLoginAsync(connection, sqlTransaction, cancellationToken);

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
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nPorcEXEC')
                ALTER TABLE dbo.ProProyectosItems ADD nPorcEXEC decimal(18, 2) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nPorcEtapa')
                ALTER TABLE dbo.ProProyectosItems ADD nPorcEtapa decimal(18, 2) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sEtapa')
                ALTER TABLE dbo.ProProyectosItems ADD sEtapa varchar(100) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosCabecera') AND name = 'nHumanoResponsable')
                ALTER TABLE dbo.ProProyectosCabecera ADD nHumanoResponsable int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nAgente')
                ALTER TABLE dbo.ProProyectosItems ADD nAgente tinyint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sEntorno')
                ALTER TABLE dbo.ProProyectosItems ADD sEntorno nvarchar(100) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'nTotalEtapas')
                ALTER TABLE dbo.ProProyectosItems ADD nTotalEtapas int NULL;
            """,

            // Identidad real de EXEC/etapa (reemplaza el matching por texto de sEtapa) +
            // agente en texto libre + referencia a quien tiene el control activo.
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'IdEXEC')
                ALTER TABLE dbo.ProProyectosItems ADD IdEXEC int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'IdEtapa')
                ALTER TABLE dbo.ProProyectosItems ADD IdEtapa int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sAgente')
                ALTER TABLE dbo.ProProyectosItems ADD sAgente nvarchar(100) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'IdControlItem')
                ALTER TABLE dbo.ProProyectosItems ADD IdControlItem bigint NULL;
            """,

            // ProProyectoEXEC / ProProyectoEtapa: entidades nuevas con identidad estable
            // (no existian antes de esta version).
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProProyectoEXEC' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE dbo.ProProyectoEXEC (
                    idTenant int NOT NULL,
                    IdProyecto int NOT NULL,
                    IdEXEC int NOT NULL,
                    sDescripcion nvarchar(500) NULL,
                    nEstado tinyint NOT NULL,
                    nTotalEtapas int NULL,
                    nHumanoResponsable int NULL,
                    dFechaInicio datetime2 NULL,
                    dFechaFin datetime2 NULL,
                    CONSTRAINT PK_ProProyectoEXEC PRIMARY KEY (idTenant, IdProyecto, IdEXEC)
                );
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProProyectoEtapa' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE dbo.ProProyectoEtapa (
                    idTenant int NOT NULL,
                    IdProyecto int NOT NULL,
                    IdEXEC int NOT NULL,
                    IdEtapa int NOT NULL,
                    nOrden int NOT NULL,
                    sDescripcion nvarchar(200) NOT NULL,
                    nPorcEtapa decimal(18, 2) NULL,
                    nEstado tinyint NOT NULL,
                    dFechaAprobacion datetime2 NULL,
                    nHumanoAprobador int NULL,
                    sCodigo varchar(50) NULL,
                    sCriteriosAceptacion nvarchar(1000) NULL,
                    IdCredentialValidador int NULL,
                    nActorTipoAprobador tinyint NULL,
                    IdCredentialAprobador int NULL,
                    CONSTRAINT PK_ProProyectoEtapa PRIMARY KEY (idTenant, IdProyecto, IdEXEC, IdEtapa)
                );
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectoEtapa') AND name = 'sCodigo')
                ALTER TABLE dbo.ProProyectoEtapa ADD sCodigo varchar(50) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectoEtapa') AND name = 'sCriteriosAceptacion')
                ALTER TABLE dbo.ProProyectoEtapa ADD sCriteriosAceptacion nvarchar(1000) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectoEtapa') AND name = 'IdCredentialValidador')
                ALTER TABLE dbo.ProProyectoEtapa ADD IdCredentialValidador int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectoEtapa') AND name = 'nActorTipoAprobador')
                ALTER TABLE dbo.ProProyectoEtapa ADD nActorTipoAprobador tinyint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectoEtapa') AND name = 'IdCredentialAprobador')
                ALTER TABLE dbo.ProProyectoEtapa ADD IdCredentialAprobador int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sCodigoEtapa')
                ALTER TABLE dbo.ProProyectosItems ADD sCodigoEtapa varchar(50) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sResultadoEtapa')
                ALTER TABLE dbo.ProProyectosItems ADD sResultadoEtapa varchar(30) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProProyectoEtapa') AND name = 'UX_ProProyectoEtapa_Tenant_Project_Exec_Code')
                CREATE UNIQUE INDEX UX_ProProyectoEtapa_Tenant_Project_Exec_Code
                    ON dbo.ProProyectoEtapa (idTenant, IdProyecto, IdEXEC, sCodigo)
                    WHERE sCodigo IS NOT NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProProyectoRoadmapRevision' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE dbo.ProProyectoRoadmapRevision (
                    idTenant int NOT NULL,
                    IdProyecto int NOT NULL,
                    IdEXEC int NOT NULL,
                    nRevision int NOT NULL,
                    IdEvento uniqueidentifier NOT NULL,
                    dFechaUtc datetime2 NOT NULL,
                    nActorId int NOT NULL,
                    IdCredential int NOT NULL,
                    sRoadmapJson nvarchar(max) NOT NULL,
                    CONSTRAINT PK_ProProyectoRoadmapRevision PRIMARY KEY (idTenant, IdProyecto, IdEXEC, nRevision),
                    CONSTRAINT UQ_ProProyectoRoadmapRevision_Event UNIQUE (idTenant, IdProyecto, IdEXEC, IdEvento)
                );
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

            // --- AURA_PROTOCOL v2.1 (AURA-PROTOCOL-V2-01): workstream + lease + credenciales ---
            // Todo aditivo/nullable; ninguna fila historica ni comportamiento existente cambia
            // mientras sWorkStream siga NULL. Ver docs/aura/AURA_PROTOCOL_v2.1.md.
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectoEXEC') AND name = 'sWorkStream')
                ALTER TABLE dbo.ProProyectoEXEC ADD sWorkStream nvarchar(100) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'sWorkStream')
                ALTER TABLE dbo.ProProyectosItems ADD sWorkStream nvarchar(100) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'LeaseId')
                ALTER TABLE dbo.ProProyectosItems ADD LeaseId uniqueidentifier NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'LeaseVersion')
                ALTER TABLE dbo.ProProyectosItems ADD LeaseVersion bigint NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'LeaseExpiresAtUtc')
                ALTER TABLE dbo.ProProyectosItems ADD LeaseExpiresAtUtc datetime2 NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProProyectosItems') AND name = 'IdCredential')
                ALTER TABLE dbo.ProProyectosItems ADD IdCredential int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AgentCredential' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE dbo.AgentCredential (
                    idTenant int NOT NULL,
                    IdCredential int NOT NULL,
                    nActorTipo tinyint NOT NULL,
                    sAgentName nvarchar(100) NOT NULL,
                    LinkedActorId int NULL,
                    SecretSalt varbinary(16) NOT NULL,
                    SecretHash varbinary(32) NOT NULL,
                    SecretIterations int NOT NULL,
                    CreatedAtUtc datetime2 NOT NULL,
                    RevokedAtUtc datetime2 NULL,
                    LastUsedAtUtc datetime2 NULL,
                    RotatedFromCredentialId int NULL,
                    CONSTRAINT PK_AgentCredential PRIMARY KEY (idTenant, IdCredential)
                );
            """,

            // --- Login básico de humanos (2026-08-28): email + contraseña sobre Usuarios ---
            // Aditivo/nullable; filas humanas existentes sin email/contraseña siguen intactas y
            // simplemente no pueden loguearse todavía.
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'sEmail')
                ALTER TABLE dbo.Usuarios ADD sEmail nvarchar(200) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'sPasswordSalt')
                ALTER TABLE dbo.Usuarios ADD sPasswordSalt varbinary(16) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'sPasswordHash')
                ALTER TABLE dbo.Usuarios ADD sPasswordHash varbinary(32) NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'nPasswordIterations')
                ALTER TABLE dbo.Usuarios ADD nPasswordIterations int NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'UX_Usuarios_Tenant_Email')
                CREATE UNIQUE INDEX UX_Usuarios_Tenant_Email
                    ON dbo.Usuarios (IdTenant, sEmail)
                    WHERE sEmail IS NOT NULL;
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

    /// <summary>
    /// Seeds the default owner login (tenant 1, IdUsuario 1) once - only when that user has no
    /// email configured yet, so a login the user later changes by hand is never overwritten by
    /// a subsequent run of this migrator. See the "login básico" request this was built for.
    /// </summary>
    private static async Task SeedDefaultHumanLoginAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const int userId = 1;
        const string name = "Arnaldo Benitez";
        const string email = "arnaldo@productorbenitez.com";
        const string password = "Cataratas3";

        await using var checkCommand = connection.CreateCommand();
        checkCommand.Transaction = transaction;
        checkCommand.CommandText = "SELECT sEmail FROM dbo.Usuarios WHERE IdTenant = @tenantId AND IdUsuario = @userId;";
        checkCommand.Parameters.AddWithValue("@tenantId", TenantId);
        checkCommand.Parameters.AddWithValue("@userId", userId);
        var existingEmailResult = await checkCommand.ExecuteScalarAsync(cancellationToken);

        if (existingEmailResult is not null && existingEmailResult != DBNull.Value)
        {
            // Ya tiene login configurado (siembra previa, o cambiado a mano por el usuario):
            // nunca se pisa.
            return;
        }

        var (salt, hash) = AURA.Application.Security.Pbkdf2PasswordHasher.Hash(System.Text.Encoding.UTF8.GetBytes(password));
        var rowExists = existingEmailResult is not null;

        if (!rowExists)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO dbo.Usuarios (IdTenant, IdUsuario, sNombre, sEmail, sPasswordSalt, sPasswordHash, nPasswordIterations)
                VALUES (@tenantId, @userId, @name, @email, @salt, @hash, @iterations);
                """;
            insertCommand.Parameters.AddWithValue("@tenantId", TenantId);
            insertCommand.Parameters.AddWithValue("@userId", userId);
            insertCommand.Parameters.AddWithValue("@name", name);
            insertCommand.Parameters.AddWithValue("@email", email);
            insertCommand.Parameters.Add("@salt", System.Data.SqlDbType.VarBinary).Value = salt;
            insertCommand.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary).Value = hash;
            insertCommand.Parameters.AddWithValue("@iterations", AURA.Application.Security.Pbkdf2PasswordHasher.Iterations);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            // La fila ya existia (p. ej. como responsable/actor humano de eventos previos) pero
            // sin login todavia: se le agrega, estableciendo tambien el nombre por defecto
            // pedido para este primer usuario.
            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE dbo.Usuarios
                SET sNombre = @name, sEmail = @email, sPasswordSalt = @salt, sPasswordHash = @hash, nPasswordIterations = @iterations
                WHERE IdTenant = @tenantId AND IdUsuario = @userId;
                """;
            updateCommand.Parameters.AddWithValue("@tenantId", TenantId);
            updateCommand.Parameters.AddWithValue("@userId", userId);
            updateCommand.Parameters.AddWithValue("@name", name);
            updateCommand.Parameters.AddWithValue("@email", email);
            updateCommand.Parameters.Add("@salt", System.Data.SqlDbType.VarBinary).Value = salt;
            updateCommand.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary).Value = hash;
            updateCommand.Parameters.AddWithValue("@iterations", AURA.Application.Security.Pbkdf2PasswordHasher.Iterations);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
