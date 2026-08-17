using System.Data;
using AURA.Application.ProjectControl;
using AURA.Domain.ProjectControl;
using AURA.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AURA.Infrastructure.ProjectControl;

/// <summary>
/// Reads and writes the legacy ProProyectos* tables directly with ADO.NET. These tables
/// predate AuraDbContext's EF model and are intentionally not mapped as EF entities (see
/// ProjectControlSchemaMigrator), so plain SQL is used here instead of LINQ - it's also
/// what OUTER APPLY / locked sequential-id allocation require.
/// </summary>
public sealed class SqlProjectControlRepository(AuraDbContext dbContext) : IProjectControlRepository
{
    private const string BoardSql =
        """
        SELECT
            c.IdProyecto,
            c.sCodigo,
            c.sNombre,
            c.nEstado,
            li.IdItems,
            li.dFechaEntrada,
            li.nTipoEvento,
            li.nEstadoTrabajo,
            li.nActorTipo,
            li.nCodificador,
            li.nAnalista,
            li.nHumano,
            li.nControlTipo,
            li.nControlId,
            li.sDescripcion,
            li.sEtapa,
            li.nPorcEtapa,
            li.nPorcEXEC,
            li.sResumen,
            li.sProximaAccion,
            li.sBloqueo,
            li.sReferencia,
            li.sModelo,
            actorCoder.sNombre AS ActorCoderName,
            actorAnalyst.sNombre AS ActorAnalystName,
            actorHuman.sNombre AS ActorHumanName,
            controlCoder.sNombre AS ControlCoderName,
            controlAnalyst.sNombre AS ControlAnalystName,
            controlHuman.sNombre AS ControlHumanName
        FROM dbo.ProProyectosCabecera c
        OUTER APPLY (
            SELECT TOP (1)
                i.IdItems, i.dFechaEntrada, i.nTipoEvento, i.nEstadoTrabajo, i.nActorTipo,
                i.nCodificador, i.nAnalista, i.nHumano, i.nControlTipo, i.nControlId,
                i.sDescripcion, i.sEtapa, i.nPorcEtapa, i.nPorcEXEC,
                i.sResumen, i.sProximaAccion, i.sBloqueo, i.sReferencia, i.sModelo
            FROM dbo.ProProyectosItems i
            WHERE i.idTenant = c.idTenant AND i.IdProyecto = c.IdProyecto
            ORDER BY i.IdItems DESC
        ) li
        LEFT JOIN dbo.ProCodificadores actorCoder
            ON li.nActorTipo = 3 AND actorCoder.IdTenant = c.idTenant AND actorCoder.IdCodificador = li.nCodificador
        LEFT JOIN dbo.ProProyectosAnalistas actorAnalyst
            ON li.nActorTipo = 2 AND actorAnalyst.IdTenant = c.idTenant AND actorAnalyst.IdAnalista = li.nAnalista
        LEFT JOIN dbo.Usuarios actorHuman
            ON li.nActorTipo = 1 AND actorHuman.IdTenant = c.idTenant AND actorHuman.IdUsuario = li.nHumano
        LEFT JOIN dbo.ProCodificadores controlCoder
            ON li.nControlTipo = 3 AND controlCoder.IdTenant = c.idTenant AND controlCoder.IdCodificador = li.nControlId
        LEFT JOIN dbo.ProProyectosAnalistas controlAnalyst
            ON li.nControlTipo = 2 AND controlAnalyst.IdTenant = c.idTenant AND controlAnalyst.IdAnalista = li.nControlId
        LEFT JOIN dbo.Usuarios controlHuman
            ON li.nControlTipo = 1 AND controlHuman.IdTenant = c.idTenant AND controlHuman.IdUsuario = li.nControlId
        WHERE c.idTenant = @tenantId
        ORDER BY
            CASE c.nEstado WHEN 3 THEN 0 WHEN 1 THEN 1 ELSE 2 END,
            li.dFechaEntrada DESC,
            c.sNombre;
        """;

    public async Task<IReadOnlyList<ProjectControlProjectDto>> GetBoardAsync(int tenantId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = BoardSql;
            command.Parameters.AddWithValue("@tenantId", tenantId);

            var results = new List<ProjectControlProjectDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapProjectRow(reader));
            }

            return results;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<ProjectEventOutcome> RegisterEventAsync(ProjectEventCommand command, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();

            var projectId = await FindProjectIdAsync(connection, null, command.TenantId, command.ProjectCode, cancellationToken);
            if (projectId is null)
            {
                return new ProjectEventOutcome(ProjectEventOutcomeStatus.ProjectNotFound, null, null);
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();

            var currentLastItemId = await GetLockedLastItemIdAsync(connection, sqlTransaction, command.TenantId, projectId.Value, cancellationToken);

            var existingItemId = await FindItemIdByEventAsync(connection, sqlTransaction, command.TenantId, projectId.Value, command.EventId, cancellationToken);
            if (existingItemId is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new ProjectEventOutcome(ProjectEventOutcomeStatus.AlreadyExists, existingItemId, null);
            }

            if (currentLastItemId != command.ExpectedPreviousItemId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ProjectEventOutcome(ProjectEventOutcomeStatus.Conflict, null, currentLastItemId);
            }

            var nextItemId = (currentLastItemId ?? 0) + 1;

            await InsertEventAsync(connection, sqlTransaction, command, projectId.Value, nextItemId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ProjectEventOutcome(ProjectEventOutcomeStatus.Created, nextItemId, null);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int?> FindProjectIdAsync(
        SqlConnection connection, SqlTransaction? transaction, int tenantId, string projectCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TOP (1) IdProyecto FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId AND sCodigo = @code;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@code", projectCode);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int id ? id : null;
    }

    private static async Task<long?> GetLockedLastItemIdAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT TOP (1) IdItems
            FROM dbo.ProProyectosItems WITH (UPDLOCK, HOLDLOCK)
            WHERE idTenant = @tenantId AND IdProyecto = @projectId
            ORDER BY IdItems DESC;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long id ? id : null;
    }

    private static async Task<long?> FindItemIdByEventAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, Guid eventId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT TOP (1) IdItems FROM dbo.ProProyectosItems WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEvento = @eventId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@eventId", eventId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long id ? id : null;
    }

    private static async Task InsertEventAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ProjectEventCommand command,
        int projectId,
        long itemId,
        CancellationToken cancellationToken)
    {
        int? coder = null;
        int? analyst = null;
        int? human = null;

        switch (command.ActorType)
        {
            case ProjectActorType.Coder:
                coder = command.ActorId;
                break;
            case ProjectActorType.AiAnalyst:
                analyst = command.ActorId;
                break;
            case ProjectActorType.Human:
                human = command.ActorId;
                break;
            case ProjectActorType.Unassigned:
            default:
                break;
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            """
            INSERT INTO dbo.ProProyectosItems
                (idTenant, IdProyecto, IdItems, dFechaEntrada, nCodificador, nAnalista, nHumano, sDescripcion, OrdenInput,
                 IdEvento, IdItemAnterior, nTipoEvento, nActorTipo, nEstadoTrabajo, nControlTipo, nControlId,
                 sResumen, sProximaAccion, sBloqueo, sReferencia, sModelo)
            VALUES
                (@tenantId, @projectId, @itemId, @now, @coder, @analyst, @human, @details, NULL,
                 @eventId, @previousItemId, @eventType, @actorType, @status, @controlType, @controlId,
                 @summary, @nextAction, @blocker, @reference, @model);
            """;

        var parameters = insertCommand.Parameters;
        parameters.AddWithValue("@tenantId", command.TenantId);
        parameters.AddWithValue("@projectId", projectId);
        parameters.AddWithValue("@itemId", itemId);
        parameters.AddWithValue("@now", DateTime.UtcNow);
        parameters.Add("@coder", SqlDbType.Int).Value = (object?)coder ?? DBNull.Value;
        parameters.Add("@analyst", SqlDbType.Int).Value = (object?)analyst ?? DBNull.Value;
        parameters.Add("@human", SqlDbType.Int).Value = (object?)human ?? DBNull.Value;
        parameters.Add("@details", SqlDbType.NVarChar).Value = (object?)command.Details ?? DBNull.Value;
        parameters.AddWithValue("@eventId", command.EventId);
        parameters.Add("@previousItemId", SqlDbType.BigInt).Value = (object?)command.ExpectedPreviousItemId ?? DBNull.Value;
        parameters.Add("@eventType", SqlDbType.TinyInt).Value = (byte)command.EventType;
        parameters.Add("@actorType", SqlDbType.TinyInt).Value = (byte)command.ActorType;
        parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)command.Status;
        parameters.Add("@controlType", SqlDbType.TinyInt).Value = command.NextControlType is null ? DBNull.Value : (byte)command.NextControlType.Value;
        parameters.Add("@controlId", SqlDbType.Int).Value = (object?)command.NextControlId ?? DBNull.Value;
        parameters.Add("@summary", SqlDbType.NVarChar).Value = command.Summary;
        parameters.Add("@nextAction", SqlDbType.NVarChar).Value = (object?)command.NextAction ?? DBNull.Value;
        parameters.Add("@blocker", SqlDbType.NVarChar).Value = (object?)command.Blocker ?? DBNull.Value;
        parameters.Add("@reference", SqlDbType.NVarChar).Value = (object?)command.Reference ?? DBNull.Value;
        parameters.Add("@model", SqlDbType.NVarChar).Value = (object?)command.Model ?? DBNull.Value;

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ProjectControlProjectDto MapProjectRow(SqlDataReader reader)
    {
        var projectId = reader.GetInt32(reader.GetOrdinal("IdProyecto"));
        var projectCode = reader.IsDBNull(reader.GetOrdinal("sCodigo")) ? null : reader.GetString(reader.GetOrdinal("sCodigo"));
        var projectName = reader.GetString(reader.GetOrdinal("sNombre"));
        var projectStatusRaw = reader.IsDBNull(reader.GetOrdinal("nEstado")) ? (byte?)null : reader.GetByte(reader.GetOrdinal("nEstado"));
        var projectStatus = projectStatusRaw is byte statusByte && Enum.IsDefined(typeof(ProjectStatus), (int)statusByte)
            ? ProjectControlCodes.ToCode((ProjectStatus)statusByte)
            : "UNKNOWN";

        var lastItemId = ReadNullableInt64(reader, "IdItems");
        var lastActivityUtc = ReadNullableDateTime(reader, "dFechaEntrada");

        var eventTypeRaw = ReadNullableByte(reader, "nTipoEvento");
        var lastEventType = eventTypeRaw is byte eventByte && Enum.IsDefined(typeof(ProjectEventType), (int)eventByte)
            ? ProjectControlCodes.ToCode((ProjectEventType)eventByte)
            : null;

        var workStatusRaw = ReadNullableByte(reader, "nEstadoTrabajo");
        var workStatus = workStatusRaw is byte workByte && Enum.IsDefined(typeof(ProjectWorkStatus), (int)workByte)
            ? ProjectControlCodes.ToCode((ProjectWorkStatus)workByte)
            : null;

        var actorTypeRaw = ReadNullableByte(reader, "nActorTipo");
        string? lastActorType = null;
        string? lastActorName = null;
        string? lastModel = null;
        if (actorTypeRaw is byte actorByte && Enum.IsDefined(typeof(ProjectActorType), (int)actorByte))
        {
            var actorType = (ProjectActorType)actorByte;
            lastActorType = ProjectControlCodes.ToCode(actorType);
            lastActorName = ResolveActorName(
                reader, actorType, "nCodificador", "nAnalista", "nHumano", "ActorCoderName", "ActorAnalystName", "ActorHumanName");
            lastModel = actorType == ProjectActorType.Human ? null : ReadNullableString(reader, "sModelo");
        }

        var controlTypeRaw = ReadNullableByte(reader, "nControlTipo");
        string? currentControlType = null;
        string? currentControlName = null;
        if (controlTypeRaw is byte controlByte && Enum.IsDefined(typeof(ProjectActorType), (int)controlByte))
        {
            var controlType = (ProjectActorType)controlByte;
            currentControlType = ProjectControlCodes.ToCode(controlType);
            currentControlName = ResolveControlName(
                reader, controlType, "nControlId", "ControlCoderName", "ControlAnalystName", "ControlHumanName");
        }

        var summary = ReadNullableString(reader, "sResumen");
        var execDescription = ReadNullableString(reader, "sDescripcion");
        var currentStage = ReadNullableString(reader, "sEtapa");
        var stageProgress = ReadNullableDecimal(reader, "nPorcEtapa");
        var execProgress = ReadNullableDecimal(reader, "nPorcEXEC");
        var nextAction = ReadNullableString(reader, "sProximaAccion");
        var blocker = ReadNullableString(reader, "sBloqueo");
        var reference = ReadNullableString(reader, "sReferencia");

        return new ProjectControlProjectDto(
            projectId,
            projectCode,
            projectName,
            projectStatus,
            lastItemId,
            lastActivityUtc,
            lastEventType,
            workStatus,
            lastActorType,
            lastActorName,
            lastModel,
            currentControlType,
            currentControlName,
            execDescription,
            currentStage,
            stageProgress,
            execProgress,
            summary,
            nextAction,
            blocker,
            reference);
    }

    private static string ResolveActorName(
        SqlDataReader reader, ProjectActorType actorType, string coderIdColumn, string analystIdColumn, string humanIdColumn,
        string coderNameColumn, string analystNameColumn, string humanNameColumn)
    {
        return actorType switch
        {
            ProjectActorType.Human => FormatCatalogName(
                ReadNullableInt32(reader, humanIdColumn), ReadNullableString(reader, humanNameColumn), "Humano"),
            ProjectActorType.AiAnalyst => FormatCatalogName(
                ReadNullableInt32(reader, analystIdColumn), ReadNullableString(reader, analystNameColumn), "Analista"),
            ProjectActorType.Coder => FormatCatalogName(
                ReadNullableInt32(reader, coderIdColumn), ReadNullableString(reader, coderNameColumn), "Codificador"),
            _ => "Sin asignar",
        };
    }

    private static string ResolveControlName(
        SqlDataReader reader, ProjectActorType controlType, string controlIdColumn,
        string coderNameColumn, string analystNameColumn, string humanNameColumn)
    {
        var controlId = ReadNullableInt32(reader, controlIdColumn);
        return controlType switch
        {
            ProjectActorType.Human => FormatCatalogName(controlId, ReadNullableString(reader, humanNameColumn), "Humano"),
            ProjectActorType.AiAnalyst => FormatCatalogName(controlId, ReadNullableString(reader, analystNameColumn), "Analista"),
            ProjectActorType.Coder => FormatCatalogName(controlId, ReadNullableString(reader, coderNameColumn), "Codificador"),
            _ => "Sin asignar",
        };
    }

    private static string FormatCatalogName(int? id, string? catalogName, string fallbackLabel)
    {
        if (id is null)
        {
            return "Sin asignar";
        }

        return string.IsNullOrWhiteSpace(catalogName) ? $"{fallbackLabel} #{id}" : catalogName;
    }

    private static long? ReadNullableInt64(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static int? ReadNullableInt32(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static byte? ReadNullableByte(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetByte(ordinal);
    }

    private static DateTime? ReadNullableDateTime(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
    }

    private static decimal? ReadNullableDecimal(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static string? ReadNullableString(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
