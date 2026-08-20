using System.Data;
using AURA.Application.ProjectControl;
using AURA.Domain.ProjectControl;
using AURA.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AURA.Infrastructure.ProjectControl;

/// <summary>
/// Reads and writes the legacy ProProyectos* tables directly with ADO.NET, plus the newer
/// ProProyectoEXEC/ProProyectoEtapa tables that give EXEC/etapa a real, stable identity
/// (IdEXEC/IdEtapa) instead of matching on sEtapa text. These tables predate AuraDbContext's
/// EF model and are intentionally not mapped as EF entities, so plain SQL is used here
/// instead of LINQ - it's also what OUTER APPLY / locked sequential-id allocation require.
/// </summary>
public sealed class SqlProjectControlRepository(AuraDbContext dbContext) : IProjectControlRepository
{
    // Catalogo legacy: nombres de ProCodificadores que en realidad describen un entorno,
    // no una persona (quedaron de antes de que existieran sAgente/sEntorno). Lista fija y
    // chica a proposito - no es una heuristica amplia.
    private static readonly Dictionary<string, string> LegacyEnvironmentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vs code"] = "VS Code",
        ["windsurf"] = "Windsurf",
        ["antigravity"] = "Antigravity",
    };

    private const string BoardSql =
        """
        SELECT
            c.IdProyecto,
            c.sCodigo,
            c.sNombre,
            c.nEstado,
            c.nHumanoResponsable,
            humanResponsible.sNombre AS HumanResponsibleName,
            li.IdItems,
            li.dFechaEntrada,
            li.nTipoEvento,
            li.nEstadoTrabajo,
            li.nActorTipo,
            li.nCodificador,
            li.nAnalista,
            li.nHumano,
            li.nAgente,
            li.sAgente,
            li.sEntorno,
            li.nControlTipo,
            li.nControlId,
            li.sDescripcion,
            li.IdEXEC,
            li.IdEtapa,
            li.sEtapa,
            li.nPorcEtapa,
            li.nPorcEXEC,
            li.sResumen,
            li.sProximaAccion,
            li.sBloqueo,
            li.sReferencia,
            li.sModelo,
            execRow.nTotalEtapas AS ExecTotalStages,
            closedStages.ClosedCount,
            actorCoder.sNombre AS ActorCoderName,
            actorAnalyst.sNombre AS ActorAnalystName,
            actorHuman.sNombre AS ActorHumanName,
            controlCoder.sNombre AS ControlCoderName,
            controlAnalyst.sNombre AS ControlAnalystName,
            controlHuman.sNombre AS ControlHumanName,
            controlItem.nActorTipo AS ControlItemActorTipo,
            controlItem.nCodificador AS ControlItemCodificador,
            controlItem.nAnalista AS ControlItemAnalista,
            controlItem.nHumano AS ControlItemHumano,
            controlItem.nAgente AS ControlItemNAgente,
            controlItem.sAgente AS ControlItemSAgente,
            controlItem.sModelo AS ControlItemModelo,
            controlItem.sEntorno AS ControlItemEntorno,
            ciActorCoder.sNombre AS ControlItemActorCoderName,
            ciActorAnalyst.sNombre AS ControlItemActorAnalystName,
            ciActorHuman.sNombre AS ControlItemActorHumanName
        FROM dbo.ProProyectosCabecera c
        OUTER APPLY (
            SELECT TOP (1)
                i.IdItems, i.dFechaEntrada, i.nTipoEvento, i.nEstadoTrabajo, i.nActorTipo,
                i.nCodificador, i.nAnalista, i.nHumano, i.nAgente, i.sAgente, i.sEntorno,
                i.nControlTipo, i.nControlId, i.IdControlItem,
                i.sDescripcion, i.IdEXEC, i.IdEtapa, i.sEtapa, i.nPorcEtapa, i.nPorcEXEC,
                i.sResumen, i.sProximaAccion, i.sBloqueo, i.sReferencia, i.sModelo
            FROM dbo.ProProyectosItems i
            WHERE i.idTenant = c.idTenant AND i.IdProyecto = c.IdProyecto
            ORDER BY i.IdItems DESC
        ) li
        OUTER APPLY (
            SELECT TOP (1) e.nTotalEtapas
            FROM dbo.ProProyectoEXEC e
            WHERE e.idTenant = c.idTenant AND e.IdProyecto = c.IdProyecto AND e.IdEXEC = li.IdEXEC
        ) execRow
        OUTER APPLY (
            SELECT COUNT(*) AS ClosedCount
            FROM dbo.ProProyectoEtapa s
            WHERE s.idTenant = c.idTenant AND s.IdProyecto = c.IdProyecto AND s.IdEXEC = li.IdEXEC
              AND s.nEstado = @approvedStageStatus
        ) closedStages
        LEFT JOIN dbo.Usuarios humanResponsible
            ON humanResponsible.IdTenant = c.idTenant AND humanResponsible.IdUsuario = c.nHumanoResponsable
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
        LEFT JOIN dbo.ProProyectosItems controlItem
            ON controlItem.idTenant = c.idTenant AND controlItem.IdProyecto = c.IdProyecto AND controlItem.IdItems = li.IdControlItem
        LEFT JOIN dbo.ProCodificadores ciActorCoder
            ON controlItem.nActorTipo = 3 AND ciActorCoder.IdTenant = c.idTenant AND ciActorCoder.IdCodificador = controlItem.nCodificador
        LEFT JOIN dbo.ProProyectosAnalistas ciActorAnalyst
            ON controlItem.nActorTipo = 2 AND ciActorAnalyst.IdTenant = c.idTenant AND ciActorAnalyst.IdAnalista = controlItem.nAnalista
        LEFT JOIN dbo.Usuarios ciActorHuman
            ON controlItem.nActorTipo = 1 AND ciActorHuman.IdTenant = c.idTenant AND ciActorHuman.IdUsuario = controlItem.nHumano
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
            command.Parameters.Add("@approvedStageStatus", SqlDbType.TinyInt).Value = (byte)ProjectStageStatus.Approved;

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

            var lockedState = await GetLockedLastItemStateAsync(connection, sqlTransaction, command.TenantId, projectId.Value, cancellationToken);

            var existingItemId = await FindItemIdByEventAsync(connection, sqlTransaction, command.TenantId, projectId.Value, command.EventId, cancellationToken);
            if (existingItemId is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new ProjectEventOutcome(ProjectEventOutcomeStatus.AlreadyExists, existingItemId, null);
            }

            if (lockedState.ItemId != command.ExpectedPreviousItemId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ProjectEventOutcome(ProjectEventOutcomeStatus.Conflict, null, lockedState.ItemId);
            }

            var nextItemId = (lockedState.ItemId ?? 0) + 1;

            // 1) EXEC vigente: se crea uno nuevo si no hay ninguno o el ultimo esta Completado.
            var exec = await ResolveOrCreateExecAsync(
                connection, sqlTransaction, command.TenantId, projectId.Value, command.Details, command.TotalStages, cancellationToken);

            // 2) Etapa: por descripcion (dentro del EXEC vigente) o heredada del evento anterior
            //    (solo si ese evento anterior pertenecia al MISMO EXEC).
            var inheritedStageId = lockedState.ExecId == exec.ExecId ? lockedState.StageId : null;
            var stage = await ResolveOrCreateStageAsync(
                connection, sqlTransaction, command.TenantId, projectId.Value, exec.ExecId, command.Stage, inheritedStageId, cancellationToken);

            if (stage is not null && command.StageProgress is not null)
            {
                await UpdateStageProgressAsync(connection, sqlTransaction, command.TenantId, projectId.Value, exec.ExecId, stage.StageId, command.StageProgress.Value, cancellationToken);
                stage = stage with { Progress = command.StageProgress };
            }

            // 3) Reglas de negocio por tipo de evento (APPROVAL exige 100%, REWORK reabre,
            //    COMPLETION exige todas las etapas aprobadas).
            var transitionError = await ApplyStageTransitionAsync(
                connection, sqlTransaction, command.TenantId, projectId.Value, command, exec, stage, cancellationToken);
            if (transitionError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ProjectEventOutcome(ProjectEventOutcomeStatus.InvalidTransition, null, null, transitionError);
            }

            var execCompletedNow = command.EventType == ProjectEventType.Completion;

            // 4) nPorcEXEC: fotografia calculada ahora, grabada en este evento (no en el GET).
            var closedCount = await CountApprovedStagesAsync(connection, sqlTransaction, command.TenantId, projectId.Value, exec.ExecId, cancellationToken);
            decimal? execProgress = exec.TotalStages is > 0
                ? Math.Round((decimal)closedCount / exec.TotalStages.Value * 100m, 2, MidpointRounding.AwayFromZero)
                : null;
            if (execCompletedNow)
            {
                execProgress = 100m;
            }

            // 5) Control actual, resuelto y grabado explicitamente (no inferido despues en el GET).
            var (controlType, controlId, controlItemId) = ResolveControl(command, nextItemId, lockedState);

            var effectiveCommand = command with
            {
                NextControlType = controlType,
                NextControlId = controlId,
            };

            await InsertEventAsync(
                connection, sqlTransaction, effectiveCommand, projectId.Value, nextItemId,
                exec, stage, execProgress, controlItemId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ProjectEventOutcome(ProjectEventOutcomeStatus.Created, nextItemId, null);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<CreateProjectOutcome> CreateProjectAsync(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();

            // sp_getapplock (en vez de UPDLOCK/HOLDLOCK sobre ProProyectosCabecera) porque el caso
            // "primer proyecto de este tenant" no tiene ninguna fila existente sobre la cual tomar
            // un lock de rango; el applock serializa incondicionalmente, haya filas o no.
            await AcquireProjectCreationLockAsync(connection, sqlTransaction, command.TenantId, cancellationToken);

            var existing = await FindProjectByCodeAsync(connection, sqlTransaction, command.TenantId, command.ProjectCode, cancellationToken);
            if (existing is { } existingProject)
            {
                if (string.Equals(existingProject.ProjectName.Trim(), command.ProjectName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new CreateProjectOutcome(CreateProjectOutcomeStatus.AlreadyExists, existingProject.ProjectId);
                }

                await transaction.RollbackAsync(cancellationToken);
                return new CreateProjectOutcome(CreateProjectOutcomeStatus.Conflict, existingProject.ProjectId);
            }

            var nextProjectId = await GetNextProjectIdAsync(connection, sqlTransaction, command.TenantId, cancellationToken);

            await InsertProjectAsync(connection, sqlTransaction, command, nextProjectId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new CreateProjectOutcome(CreateProjectOutcomeStatus.Created, nextProjectId);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private const string HistorySql =
        """
        SELECT
            i.IdItems, i.dFechaEntrada, i.nTipoEvento, i.nEstadoTrabajo, i.nActorTipo,
            i.nCodificador, i.nAnalista, i.nHumano, i.nAgente, i.sAgente, i.sEntorno,
            i.nControlTipo, i.nControlId,
            i.sDescripcion, i.IdEXEC, i.IdEtapa, i.sEtapa, i.nPorcEtapa, i.nPorcEXEC,
            i.sResumen, i.sProximaAccion, i.sBloqueo, i.sReferencia, i.sModelo,
            actorCoder.sNombre AS ActorCoderName,
            actorAnalyst.sNombre AS ActorAnalystName,
            actorHuman.sNombre AS ActorHumanName,
            controlCoder.sNombre AS ControlCoderName,
            controlAnalyst.sNombre AS ControlAnalystName,
            controlHuman.sNombre AS ControlHumanName
        FROM dbo.ProProyectosItems i
        LEFT JOIN dbo.ProCodificadores actorCoder
            ON i.nActorTipo = 3 AND actorCoder.IdTenant = i.idTenant AND actorCoder.IdCodificador = i.nCodificador
        LEFT JOIN dbo.ProProyectosAnalistas actorAnalyst
            ON i.nActorTipo = 2 AND actorAnalyst.IdTenant = i.idTenant AND actorAnalyst.IdAnalista = i.nAnalista
        LEFT JOIN dbo.Usuarios actorHuman
            ON i.nActorTipo = 1 AND actorHuman.IdTenant = i.idTenant AND actorHuman.IdUsuario = i.nHumano
        LEFT JOIN dbo.ProCodificadores controlCoder
            ON i.nControlTipo = 3 AND controlCoder.IdTenant = i.idTenant AND controlCoder.IdCodificador = i.nControlId
        LEFT JOIN dbo.ProProyectosAnalistas controlAnalyst
            ON i.nControlTipo = 2 AND controlAnalyst.IdTenant = i.idTenant AND controlAnalyst.IdAnalista = i.nControlId
        LEFT JOIN dbo.Usuarios controlHuman
            ON i.nControlTipo = 1 AND controlHuman.IdTenant = i.idTenant AND controlHuman.IdUsuario = i.nControlId
        WHERE i.idTenant = @tenantId AND i.IdProyecto = @projectId
        ORDER BY i.IdItems ASC;
        """;

    public async Task<ProjectHistoryDto?> GetHistoryAsync(int tenantId, string projectCode, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();

            var project = await FindProjectByCodeAsync(connection, null, tenantId, projectCode, cancellationToken);
            if (project is not { } found)
            {
                return null;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = HistorySql;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@projectId", found.ProjectId);

            var interventions = new List<ProjectInterventionDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                interventions.Add(MapInterventionRow(reader));
            }

            return new ProjectHistoryDto(found.ProjectId, projectCode, found.ProjectName, interventions);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static ProjectInterventionDto MapInterventionRow(SqlDataReader reader)
    {
        var itemId = reader.GetInt64(reader.GetOrdinal("IdItems"));
        var timestampUtc = ReadNullableDateTime(reader, "dFechaEntrada");

        var eventTypeRaw = ReadNullableByte(reader, "nTipoEvento");
        var eventType = eventTypeRaw is byte eventByte && Enum.IsDefined(typeof(ProjectEventType), (int)eventByte)
            ? ProjectControlCodes.ToCode((ProjectEventType)eventByte)
            : null;

        var workStatusRaw = ReadNullableByte(reader, "nEstadoTrabajo");
        var workStatus = workStatusRaw is byte workByte && Enum.IsDefined(typeof(ProjectWorkStatus), (int)workByte)
            ? ProjectControlCodes.ToCode((ProjectWorkStatus)workByte)
            : null;

        var sAgente = ReadNullableString(reader, "sAgente");
        var nAgenteRaw = ReadNullableByte(reader, "nAgente");
        var agent = sAgente ?? (nAgenteRaw is byte agentByte && Enum.IsDefined(typeof(ProjectAgent), (int)agentByte)
            ? ProjectControlCodes.ToCode((ProjectAgent)agentByte)
            : null);
        var environment = ReadNullableString(reader, "sEntorno");

        var actorTypeRaw = ReadNullableByte(reader, "nActorTipo");
        string? actorType = null;
        string? actorName = null;
        string? model = null;
        if (actorTypeRaw is byte actorByte && Enum.IsDefined(typeof(ProjectActorType), (int)actorByte))
        {
            var parsedActorType = (ProjectActorType)actorByte;
            actorType = ProjectControlCodes.ToCode(parsedActorType);
            actorName = ResolveActorName(
                reader, parsedActorType, "nCodificador", "nAnalista", "nHumano", "ActorCoderName", "ActorAnalystName", "ActorHumanName");
            model = parsedActorType == ProjectActorType.Human ? null : ReadNullableString(reader, "sModelo");

            (actorName, environment) = ApplyLegacyEnvironmentFallback(parsedActorType, actorName, environment);
        }

        var controlTypeRaw = ReadNullableByte(reader, "nControlTipo");
        string? nextControlType = null;
        string? nextControlName = null;
        if (controlTypeRaw is byte controlByte && Enum.IsDefined(typeof(ProjectActorType), (int)controlByte))
        {
            var parsedControlType = (ProjectActorType)controlByte;
            nextControlType = ProjectControlCodes.ToCode(parsedControlType);
            nextControlName = ResolveControlName(
                reader, parsedControlType, "nControlId", "ControlCoderName", "ControlAnalystName", "ControlHumanName");
        }

        return new ProjectInterventionDto(
            itemId,
            timestampUtc,
            eventType,
            actorType,
            actorName,
            agent,
            model,
            environment,
            workStatus,
            ReadNullableString(reader, "sDescripcion"),
            ReadNullableInt32(reader, "IdEXEC"),
            ReadNullableInt32(reader, "IdEtapa"),
            ReadNullableString(reader, "sEtapa"),
            ReadNullableDecimal(reader, "nPorcEtapa"),
            ReadNullableDecimal(reader, "nPorcEXEC"),
            nextControlType,
            nextControlName,
            ReadNullableString(reader, "sResumen"),
            ReadNullableString(reader, "sProximaAccion"),
            ReadNullableString(reader, "sBloqueo"),
            ReadNullableString(reader, "sReferencia"));
    }

    private static async Task AcquireProjectCreationLockAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;";
        command.Parameters.AddWithValue("@resource", $"ProProyectosCabecera:{tenantId}");
        var resultParameter = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        command.Parameters.Add(resultParameter);

        await command.ExecuteNonQueryAsync(cancellationToken);

        var lockResult = (int)resultParameter.Value;
        if (lockResult < 0)
        {
            throw new InvalidOperationException(
                $"No se pudo adquirir el lock de creacion de proyectos para el tenant {tenantId} (sp_getapplock devolvio {lockResult}).");
        }
    }

    private static async Task<(int ProjectId, string ProjectName)?> FindProjectByCodeAsync(
        SqlConnection connection, SqlTransaction? transaction, int tenantId, string projectCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TOP (1) IdProyecto, sNombre FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId AND sCodigo = @code;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@code", projectCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (reader.GetInt32(0), reader.GetString(1));
    }

    private static async Task<int> GetNextProjectIdAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ISNULL(MAX(IdProyecto), 0) FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) + 1;
    }

    private static async Task InsertProjectAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CreateProjectCommand command,
        int projectId,
        CancellationToken cancellationToken)
    {
        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            """
            INSERT INTO dbo.ProProyectosCabecera
                (idTenant, IdProyecto, sNombre, sDescripcion, dFecCreacion, sPathTrabajo, nEstado, sCodigo, sRepositorio, nHumanoResponsable)
            VALUES
                (@tenantId, @projectId, @name, @description, CAST(SYSUTCDATETIME() AS date), @workingPath, @status, @code, @repository, @humanResponsibleId);
            """;

        var parameters = insertCommand.Parameters;
        parameters.AddWithValue("@tenantId", command.TenantId);
        parameters.AddWithValue("@projectId", projectId);
        parameters.AddWithValue("@name", command.ProjectName);
        parameters.Add("@description", SqlDbType.VarChar).Value = (object?)command.Description ?? DBNull.Value;
        parameters.Add("@workingPath", SqlDbType.Text).Value = (object?)command.WorkingPath ?? DBNull.Value;
        parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)command.Status;
        parameters.AddWithValue("@code", command.ProjectCode);
        parameters.Add("@repository", SqlDbType.NVarChar).Value = (object?)command.Repository ?? DBNull.Value;
        parameters.Add("@humanResponsibleId", SqlDbType.Int).Value = (object?)command.HumanResponsibleId ?? DBNull.Value;

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
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

    private static async Task<int?> GetProjectHumanResponsibleIdAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT nHumanoResponsable FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId AND IdProyecto = @projectId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int id ? id : null;
    }

    private sealed record LockedItemState(long? ItemId, int? ExecId, int? StageId, long? ControlItemId);

    private static async Task<LockedItemState> GetLockedLastItemStateAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT TOP (1) IdItems, IdEXEC, IdEtapa, IdControlItem
            FROM dbo.ProProyectosItems WITH (UPDLOCK, HOLDLOCK)
            WHERE idTenant = @tenantId AND IdProyecto = @projectId
            ORDER BY IdItems DESC;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new LockedItemState(null, null, null, null);
        }

        return new LockedItemState(
            reader.IsDBNull(0) ? null : reader.GetInt64(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt64(3));
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

    // ---- EXEC ----

    private sealed record ExecRow(int ExecId, string? Description, ProjectExecStatus Status, int? TotalStages);

    private static async Task<ExecRow?> GetLatestExecAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT TOP (1) IdEXEC, sDescripcion, nEstado, nTotalEtapas FROM dbo.ProProyectoEXEC " +
            "WHERE idTenant = @tenantId AND IdProyecto = @projectId ORDER BY IdEXEC DESC;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExecRow(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            (ProjectExecStatus)reader.GetByte(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3));
    }

    private static async Task<ExecRow> ResolveOrCreateExecAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId,
        string? requestedDescription, int? requestedTotalStages, CancellationToken cancellationToken)
    {
        var current = await GetLatestExecAsync(connection, transaction, tenantId, projectId, cancellationToken);

        if (current is null || current.Status == ProjectExecStatus.Completed)
        {
            // EXEC nuevo: sin mezclar etapas/aprobaciones del anterior, arranca en 0.
            var newExecId = (current?.ExecId ?? 0) + 1;
            var humanResponsibleId = await GetProjectHumanResponsibleIdAsync(connection, transaction, tenantId, projectId, cancellationToken);

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO dbo.ProProyectoEXEC (idTenant, IdProyecto, IdEXEC, sDescripcion, nEstado, nTotalEtapas, nHumanoResponsable, dFechaInicio, dFechaFin)
                VALUES (@tenantId, @projectId, @execId, @description, @status, @totalStages, @humanResponsibleId, @now, NULL);
                """;
            insert.Parameters.AddWithValue("@tenantId", tenantId);
            insert.Parameters.AddWithValue("@projectId", projectId);
            insert.Parameters.AddWithValue("@execId", newExecId);
            insert.Parameters.Add("@description", SqlDbType.NVarChar).Value = (object?)requestedDescription ?? DBNull.Value;
            insert.Parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)ProjectExecStatus.Active;
            insert.Parameters.Add("@totalStages", SqlDbType.Int).Value = (object?)requestedTotalStages ?? DBNull.Value;
            insert.Parameters.Add("@humanResponsibleId", SqlDbType.Int).Value = (object?)humanResponsibleId ?? DBNull.Value;
            insert.Parameters.AddWithValue("@now", DateTime.UtcNow);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            return new ExecRow(newExecId, requestedDescription, ProjectExecStatus.Active, requestedTotalStages);
        }

        // Continua el EXEC vigente. La descripcion estable y el plan de etapas se heredan
        // cuando el evento no los manda (nunca desaparecen por un CLAIM/PROGRESS/etc).
        var effectiveDescription = requestedDescription ?? current.Description;
        var effectiveTotalStages = requestedTotalStages ?? current.TotalStages;

        if (requestedDescription is not null || requestedTotalStages is not null)
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                "UPDATE dbo.ProProyectoEXEC SET sDescripcion = @description, nTotalEtapas = @totalStages " +
                "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId;";
            update.Parameters.Add("@description", SqlDbType.NVarChar).Value = (object?)effectiveDescription ?? DBNull.Value;
            update.Parameters.Add("@totalStages", SqlDbType.Int).Value = (object?)effectiveTotalStages ?? DBNull.Value;
            update.Parameters.AddWithValue("@tenantId", tenantId);
            update.Parameters.AddWithValue("@projectId", projectId);
            update.Parameters.AddWithValue("@execId", current.ExecId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        return current with { Description = effectiveDescription, TotalStages = effectiveTotalStages };
    }

    // ---- Etapa ----

    private sealed record StageRow(int StageId, string Description, decimal? Progress, ProjectStageStatus Status);

    private static async Task<StageRow?> FindStageByDescriptionAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, int execId, string description, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT TOP (1) IdEtapa, sDescripcion, nPorcEtapa, nEstado FROM dbo.ProProyectoEtapa " +
            "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId AND LOWER(sDescripcion) = LOWER(@description);";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@execId", execId);
        command.Parameters.AddWithValue("@description", description);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StageRow(
            reader.GetInt32(0), reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDecimal(2),
            (ProjectStageStatus)reader.GetByte(3));
    }

    private static async Task<StageRow?> GetStageByIdAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, int execId, int stageId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT IdEtapa, sDescripcion, nPorcEtapa, nEstado FROM dbo.ProProyectoEtapa " +
            "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId AND IdEtapa = @stageId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@execId", execId);
        command.Parameters.AddWithValue("@stageId", stageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StageRow(
            reader.GetInt32(0), reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDecimal(2),
            (ProjectStageStatus)reader.GetByte(3));
    }

    private static async Task<StageRow?> ResolveOrCreateStageAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, int execId,
        string? requestedStageDescription, int? inheritedStageId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedStageDescription))
        {
            var trimmed = requestedStageDescription.Trim();
            var existing = await FindStageByDescriptionAsync(connection, transaction, tenantId, projectId, execId, trimmed, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText =
                "SELECT ISNULL(MAX(IdEtapa), 0), COUNT(*) FROM dbo.ProProyectoEtapa " +
                "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId;";
            countCommand.Parameters.AddWithValue("@tenantId", tenantId);
            countCommand.Parameters.AddWithValue("@projectId", projectId);
            countCommand.Parameters.AddWithValue("@execId", execId);

            int nextStageId;
            int nextOrden;
            await using (var reader = await countCommand.ExecuteReaderAsync(cancellationToken))
            {
                await reader.ReadAsync(cancellationToken);
                nextStageId = reader.GetInt32(0) + 1;
                nextOrden = reader.GetInt32(1) + 1;
            }

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO dbo.ProProyectoEtapa (idTenant, IdProyecto, IdEXEC, IdEtapa, nOrden, sDescripcion, nPorcEtapa, nEstado, dFechaAprobacion, nHumanoAprobador)
                VALUES (@tenantId, @projectId, @execId, @stageId, @orden, @description, NULL, @status, NULL, NULL);
                """;
            insert.Parameters.AddWithValue("@tenantId", tenantId);
            insert.Parameters.AddWithValue("@projectId", projectId);
            insert.Parameters.AddWithValue("@execId", execId);
            insert.Parameters.AddWithValue("@stageId", nextStageId);
            insert.Parameters.AddWithValue("@orden", nextOrden);
            insert.Parameters.AddWithValue("@description", trimmed);
            insert.Parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)ProjectStageStatus.Open;
            await insert.ExecuteNonQueryAsync(cancellationToken);

            return new StageRow(nextStageId, trimmed, null, ProjectStageStatus.Open);
        }

        if (inheritedStageId is not null)
        {
            return await GetStageByIdAsync(connection, transaction, tenantId, projectId, execId, inheritedStageId.Value, cancellationToken);
        }

        return null;
    }

    private static async Task UpdateStageProgressAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, int execId, int stageId, decimal progress, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE dbo.ProProyectoEtapa SET nPorcEtapa = @progress " +
            "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId AND IdEtapa = @stageId;";
        var progressParam = command.Parameters.Add("@progress", SqlDbType.Decimal);
        progressParam.Precision = 18;
        progressParam.Scale = 2;
        progressParam.Value = progress;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@execId", execId);
        command.Parameters.AddWithValue("@stageId", stageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> CountApprovedStagesAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId, int execId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT COUNT(*) FROM dbo.ProProyectoEtapa WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId AND nEstado = @approved;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@execId", execId);
        command.Parameters.Add("@approved", SqlDbType.TinyInt).Value = (byte)ProjectStageStatus.Approved;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Aplica las reglas de APPROVAL/REWORK/COMPLETION. Devuelve un mensaje de error si la
    /// transicion no es valida (y no escribe nada); null si es valida (y ya aplico el UPDATE).
    /// </summary>
    private static async Task<string?> ApplyStageTransitionAsync(
        SqlConnection connection, SqlTransaction transaction, int tenantId, int projectId,
        ProjectEventCommand command, ExecRow exec, StageRow? stage, CancellationToken cancellationToken)
    {
        switch (command.EventType)
        {
            case ProjectEventType.Approval:
            {
                if (stage is null)
                {
                    return "No se pudo identificar la etapa a aprobar.";
                }

                if (stage.Progress != 100m)
                {
                    return "La etapa debe estar al 100% de avance tecnico antes de aprobarla.";
                }

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText =
                    "UPDATE dbo.ProProyectoEtapa SET nEstado = @approved, dFechaAprobacion = @now, nHumanoAprobador = @humanId " +
                    "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId AND IdEtapa = @stageId;";
                update.Parameters.Add("@approved", SqlDbType.TinyInt).Value = (byte)ProjectStageStatus.Approved;
                update.Parameters.AddWithValue("@now", DateTime.UtcNow);
                update.Parameters.Add("@humanId", SqlDbType.Int).Value = (object?)command.ActorId ?? DBNull.Value;
                update.Parameters.AddWithValue("@tenantId", tenantId);
                update.Parameters.AddWithValue("@projectId", projectId);
                update.Parameters.AddWithValue("@execId", exec.ExecId);
                update.Parameters.AddWithValue("@stageId", stage.StageId);
                await update.ExecuteNonQueryAsync(cancellationToken);
                return null;
            }

            case ProjectEventType.Rework:
            {
                if (stage is null)
                {
                    return "No se pudo identificar la etapa a reabrir.";
                }

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText =
                    "UPDATE dbo.ProProyectoEtapa SET nEstado = @open, dFechaAprobacion = NULL, nHumanoAprobador = NULL " +
                    "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId AND IdEtapa = @stageId;";
                update.Parameters.Add("@open", SqlDbType.TinyInt).Value = (byte)ProjectStageStatus.Open;
                update.Parameters.AddWithValue("@tenantId", tenantId);
                update.Parameters.AddWithValue("@projectId", projectId);
                update.Parameters.AddWithValue("@execId", exec.ExecId);
                update.Parameters.AddWithValue("@stageId", stage.StageId);
                await update.ExecuteNonQueryAsync(cancellationToken);
                return null;
            }

            case ProjectEventType.Completion:
            {
                if (exec.TotalStages is not > 0)
                {
                    return "No se puede completar el EXEC sin un total de etapas definido.";
                }

                var approvedCount = await CountApprovedStagesAsync(connection, transaction, tenantId, projectId, exec.ExecId, cancellationToken);
                if (approvedCount < exec.TotalStages.Value)
                {
                    return $"No todas las etapas estan aprobadas ({approvedCount} de {exec.TotalStages.Value}).";
                }

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText =
                    "UPDATE dbo.ProProyectoEXEC SET nEstado = @completed, dFechaFin = @now " +
                    "WHERE idTenant = @tenantId AND IdProyecto = @projectId AND IdEXEC = @execId;";
                update.Parameters.Add("@completed", SqlDbType.TinyInt).Value = (byte)ProjectExecStatus.Completed;
                update.Parameters.AddWithValue("@now", DateTime.UtcNow);
                update.Parameters.AddWithValue("@tenantId", tenantId);
                update.Parameters.AddWithValue("@projectId", projectId);
                update.Parameters.AddWithValue("@execId", exec.ExecId);
                await update.ExecuteNonQueryAsync(cancellationToken);
                return null;
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Control actual, resuelto explicitamente en la escritura (no inferido despues en el GET):
    /// CLAIM siempre pasa el control al propio actor (ignora cualquier nextControl que venga en
    /// el mismo request - tomar y ceder el control en el mismo evento no tiene sentido). Un
    /// nextControl explicito (tipicamente HANDOFF) es la unica otra forma de cambiarlo. Si no
    /// pasa ninguna de las dos cosas, se hereda el control del evento anterior.
    /// </summary>
    private static (ProjectActorType? ControlType, int? ControlId, long? ControlItemId) ResolveControl(
        ProjectEventCommand command, long newItemId, LockedItemState lockedState)
    {
        if (command.EventType == ProjectEventType.Claim)
        {
            return (null, null, newItemId);
        }

        if (command.NextControlType is not null)
        {
            return (command.NextControlType, command.NextControlId, null);
        }

        return (null, null, lockedState.ControlItemId);
    }

    private static async Task InsertEventAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ProjectEventCommand command,
        int projectId,
        long itemId,
        ExecRow exec,
        StageRow? stage,
        decimal? execProgress,
        long? controlItemId,
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
                 IdEvento, IdItemAnterior, nTipoEvento, nActorTipo, sAgente, sEntorno, nEstadoTrabajo, nControlTipo, nControlId, IdControlItem,
                 sResumen, sProximaAccion, sBloqueo, sReferencia, sModelo, IdEXEC, IdEtapa, sEtapa, nPorcEtapa, nTotalEtapas, nPorcEXEC)
            VALUES
                (@tenantId, @projectId, @itemId, @now, @coder, @analyst, @human, @details, NULL,
                 @eventId, @previousItemId, @eventType, @actorType, @agent, @environment, @status, @controlType, @controlId, @controlItemId,
                 @summary, @nextAction, @blocker, @reference, @model, @execId, @stageId, @stageDescription, @stageProgress, @totalStages, @execProgress);
            """;

        var parameters = insertCommand.Parameters;
        parameters.AddWithValue("@tenantId", command.TenantId);
        parameters.AddWithValue("@projectId", projectId);
        parameters.AddWithValue("@itemId", itemId);
        parameters.AddWithValue("@now", DateTime.UtcNow);
        parameters.Add("@coder", SqlDbType.Int).Value = (object?)coder ?? DBNull.Value;
        parameters.Add("@analyst", SqlDbType.Int).Value = (object?)analyst ?? DBNull.Value;
        parameters.Add("@human", SqlDbType.Int).Value = (object?)human ?? DBNull.Value;
        // sDescripcion (legacy, denormalizada) siempre refleja la descripcion estable del EXEC vigente.
        parameters.Add("@details", SqlDbType.NVarChar).Value = (object?)exec.Description ?? DBNull.Value;
        parameters.AddWithValue("@eventId", command.EventId);
        parameters.Add("@previousItemId", SqlDbType.BigInt).Value = (object?)command.ExpectedPreviousItemId ?? DBNull.Value;
        parameters.Add("@eventType", SqlDbType.TinyInt).Value = (byte)command.EventType;
        parameters.Add("@actorType", SqlDbType.TinyInt).Value = (byte)command.ActorType;
        parameters.Add("@agent", SqlDbType.NVarChar).Value = (object?)command.Agent ?? DBNull.Value;
        parameters.Add("@environment", SqlDbType.NVarChar).Value = (object?)command.Environment ?? DBNull.Value;
        parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)command.Status;
        parameters.Add("@controlType", SqlDbType.TinyInt).Value = command.NextControlType is null ? DBNull.Value : (byte)command.NextControlType.Value;
        parameters.Add("@controlId", SqlDbType.Int).Value = (object?)command.NextControlId ?? DBNull.Value;
        parameters.Add("@controlItemId", SqlDbType.BigInt).Value = (object?)controlItemId ?? DBNull.Value;
        parameters.Add("@summary", SqlDbType.NVarChar).Value = command.Summary;
        parameters.Add("@nextAction", SqlDbType.NVarChar).Value = (object?)command.NextAction ?? DBNull.Value;
        parameters.Add("@blocker", SqlDbType.NVarChar).Value = (object?)command.Blocker ?? DBNull.Value;
        parameters.Add("@reference", SqlDbType.NVarChar).Value = (object?)command.Reference ?? DBNull.Value;
        parameters.Add("@model", SqlDbType.NVarChar).Value = (object?)command.Model ?? DBNull.Value;
        parameters.AddWithValue("@execId", exec.ExecId);
        parameters.Add("@stageId", SqlDbType.Int).Value = (object?)stage?.StageId ?? DBNull.Value;
        parameters.Add("@stageDescription", SqlDbType.VarChar).Value = (object?)stage?.Description ?? DBNull.Value;

        var stageProgressParam = parameters.Add("@stageProgress", SqlDbType.Decimal);
        stageProgressParam.Precision = 18;
        stageProgressParam.Scale = 2;
        stageProgressParam.Value = (object?)stage?.Progress ?? DBNull.Value;

        parameters.Add("@totalStages", SqlDbType.Int).Value = (object?)exec.TotalStages ?? DBNull.Value;

        var execProgressParam = parameters.Add("@execProgress", SqlDbType.Decimal);
        execProgressParam.Precision = 18;
        execProgressParam.Scale = 2;
        execProgressParam.Value = (object?)execProgress ?? DBNull.Value;

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Si el catalogo de ProCodificadores dice que este "codificador" en realidad es un
    /// entorno (Vs Code, Windsurf, Antigravity), nunca se muestra como si fuera una persona -
    /// tenga o no la fila, ademas, su propio agente/modelo/entorno explicitos (ambas cosas
    /// pueden convivir: id 1 del catalogo real es literalmente "Vs Code", y un evento sobre
    /// ese id puede llegar con sAgente/sEntorno propios de una intervencion real).
    /// </summary>
    private static (string? Name, string? Environment) ApplyLegacyEnvironmentFallback(
        ProjectActorType actorType, string? actorName, string? explicitEnvironment)
    {
        if (actorType != ProjectActorType.Coder
            || actorName is null
            || !LegacyEnvironmentNames.TryGetValue(actorName.Trim(), out var normalized))
        {
            return (actorName, explicitEnvironment);
        }

        return (null, explicitEnvironment ?? normalized);
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

        var humanResponsibleId = ReadNullableInt32(reader, "nHumanoResponsable");
        var humanResponsibleName = ReadNullableString(reader, "HumanResponsibleName");

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

        var sAgente = ReadNullableString(reader, "sAgente");
        var nAgenteRaw = ReadNullableByte(reader, "nAgente");
        var lastAgent = sAgente ?? (nAgenteRaw is byte agentByte && Enum.IsDefined(typeof(ProjectAgent), (int)agentByte)
            ? ProjectControlCodes.ToCode((ProjectAgent)agentByte)
            : null);
        var lastEnvironment = ReadNullableString(reader, "sEntorno");

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

            (lastActorName, lastEnvironment) = ApplyLegacyEnvironmentFallback(actorType, lastActorName, lastEnvironment);
        }

        // Control actual: prioridad 1) IdControlItem resuelto en la escritura (cubre CLAIM y
        // continuidad heredada, con agente/modelo/entorno completos via el join a controlItem);
        // 2) nControlTipo explicito de este evento (handoff a humano, tipicamente);
        // 3) fallback legacy al propio actor de esta fila (filas de antes de que IdControlItem existiera).
        string? currentControlType;
        string? currentControlName;
        string? currentControlAgent = null;
        string? currentControlModel = null;
        string? currentControlEnvironment = null;

        var controlItemActorTypeRaw = ReadNullableByte(reader, "ControlItemActorTipo");
        var controlTypeRaw = ReadNullableByte(reader, "nControlTipo");

        if (controlItemActorTypeRaw is byte ciActorByte && Enum.IsDefined(typeof(ProjectActorType), (int)ciActorByte))
        {
            var controlActorType = (ProjectActorType)ciActorByte;
            currentControlType = ProjectControlCodes.ToCode(controlActorType);
            currentControlName = ResolveActorName(
                reader, controlActorType, "ControlItemCodificador", "ControlItemAnalista", "ControlItemHumano",
                "ControlItemActorCoderName", "ControlItemActorAnalystName", "ControlItemActorHumanName");

            var ciSAgente = ReadNullableString(reader, "ControlItemSAgente");
            var ciNAgenteRaw = ReadNullableByte(reader, "ControlItemNAgente");
            currentControlAgent = ciSAgente ?? (ciNAgenteRaw is byte ciAgentByte && Enum.IsDefined(typeof(ProjectAgent), (int)ciAgentByte)
                ? ProjectControlCodes.ToCode((ProjectAgent)ciAgentByte)
                : null);
            currentControlModel = controlActorType == ProjectActorType.Human ? null : ReadNullableString(reader, "ControlItemModelo");
            currentControlEnvironment = ReadNullableString(reader, "ControlItemEntorno");

            (currentControlName, currentControlEnvironment) = ApplyLegacyEnvironmentFallback(
                controlActorType, currentControlName, currentControlEnvironment);
        }
        else if (controlTypeRaw is byte controlByte && Enum.IsDefined(typeof(ProjectActorType), (int)controlByte))
        {
            var controlType = (ProjectActorType)controlByte;
            currentControlType = ProjectControlCodes.ToCode(controlType);
            currentControlName = ResolveControlName(
                reader, controlType, "nControlId", "ControlCoderName", "ControlAnalystName", "ControlHumanName");
        }
        else
        {
            currentControlType = lastActorType;
            currentControlName = lastActorName;
            currentControlAgent = lastAgent;
            currentControlModel = lastModel;
            currentControlEnvironment = lastEnvironment;
        }

        var summary = ReadNullableString(reader, "sResumen");
        var execDescription = ReadNullableString(reader, "sDescripcion");
        var execId = ReadNullableInt32(reader, "IdEXEC");
        var stageId = ReadNullableInt32(reader, "IdEtapa");
        var currentStage = ReadNullableString(reader, "sEtapa");
        var stageProgress = ReadNullableDecimal(reader, "nPorcEtapa");
        var nextAction = ReadNullableString(reader, "sProximaAccion");
        var blocker = ReadNullableString(reader, "sBloqueo");
        var reference = ReadNullableString(reader, "sReferencia");

        var totalStages = ReadNullableInt32(reader, "ExecTotalStages");
        var closedStages = reader.GetInt32(reader.GetOrdinal("ClosedCount"));
        // nPorcEXEC ya es una fotografia calculada y grabada en la escritura - se lee tal cual.
        var execProgress = ReadNullableDecimal(reader, "nPorcEXEC");

        return new ProjectControlProjectDto(
            projectId,
            projectCode,
            projectName,
            projectStatus,
            humanResponsibleId,
            humanResponsibleName,
            lastItemId,
            lastActivityUtc,
            lastEventType,
            workStatus,
            lastActorType,
            lastActorName,
            lastAgent,
            lastModel,
            lastEnvironment,
            currentControlType,
            currentControlName,
            currentControlAgent,
            currentControlModel,
            currentControlEnvironment,
            execId,
            execDescription,
            stageId,
            currentStage,
            stageProgress,
            totalStages,
            closedStages,
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
