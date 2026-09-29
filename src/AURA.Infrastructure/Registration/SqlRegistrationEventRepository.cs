using System.Data;
using AURA.Application.Registration;
using AURA.Domain.Registration;
using AURA.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AURA.Infrastructure.Registration;

public sealed class SqlRegistrationEventRepository(AuraDbContext dbContext) : IRegistrationEventRepository
{
    public async Task<RegistrationEventOutcome> RegisterAsync(
        RegistrationEventCommand command,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            if (!await CanCredentialRegisterForProjectAsync(connection, sqlTransaction, command.TenantId, command.CredentialId, command.ProjectCode, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RegistrationEventOutcome(RegistrationEventStatus.Forbidden, null, null);
            }
            var projectId = await FindProjectIdAsync(connection, sqlTransaction, command.TenantId, command.ProjectCode, cancellationToken);
            var projectResolution = projectId is null
                ? ProjectRegistrationResolution.Provisional
                : ProjectRegistrationResolution.Resolved;

            try
            {
                await using var insert = connection.CreateCommand();
                insert.Transaction = sqlTransaction;
                insert.CommandText =
                    """
                    INSERT INTO dbo.AgentRegistrationEvent
                        (IdEvento, idTenant, IdProyecto, sCodigoProyecto, sExternalProjectKey, sProjectName,
                         sMode, sTipoEvento, sResumen, sDetalles, sClaimedActorType, nClaimedActorId,
                         sAgentName, sHost, sWorkspace, SessionId, sModel, sEnvironment, sSource,
                         OccurredAtUtc, RegisteredAtUtc, sIdentityStatus, sProjectResolution, IdCredential)
                    OUTPUT INSERTED.IdItem
                    VALUES
                        (@eventId, @tenantId, @projectId, @projectCode, @externalProjectKey, @projectName,
                         @mode, @eventType, @summary, @details, @claimedActorType, @claimedActorId,
                         @agentName, @host, @workspace, @sessionId, @model, @environment, @source,
                         @occurredAtUtc, @registeredAtUtc, 'UNVERIFIED', @projectResolution, @credentialId);
                    """;

                var parameters = insert.Parameters;
                parameters.Add("@eventId", SqlDbType.UniqueIdentifier).Value = command.EventId;
                parameters.AddWithValue("@tenantId", command.TenantId);
                parameters.Add("@projectId", SqlDbType.Int).Value = (object?)projectId ?? DBNull.Value;
                parameters.Add("@projectCode", SqlDbType.VarChar, 50).Value = command.ProjectCode;
                parameters.Add("@externalProjectKey", SqlDbType.NVarChar, 200).Value = (object?)command.ExternalProjectKey ?? DBNull.Value;
                parameters.Add("@projectName", SqlDbType.NVarChar, 200).Value = (object?)command.ProjectName ?? DBNull.Value;
                parameters.Add("@mode", SqlDbType.VarChar, 30).Value = command.Mode;
                parameters.Add("@eventType", SqlDbType.VarChar, 30).Value = ToCode(command.EventType);
                parameters.Add("@summary", SqlDbType.NVarChar, 300).Value = command.Summary;
                parameters.Add("@details", SqlDbType.NVarChar, 4000).Value = (object?)command.Details ?? DBNull.Value;
                parameters.Add("@claimedActorType", SqlDbType.VarChar, 30).Value = (object?)command.ClaimedActorType ?? DBNull.Value;
                parameters.Add("@claimedActorId", SqlDbType.Int).Value = (object?)command.ClaimedActorId ?? DBNull.Value;
                parameters.Add("@agentName", SqlDbType.NVarChar, 100).Value = (object?)command.AgentName ?? DBNull.Value;
                parameters.Add("@host", SqlDbType.NVarChar, 100).Value = (object?)command.Host ?? DBNull.Value;
                parameters.Add("@workspace", SqlDbType.NVarChar, 300).Value = (object?)command.Workspace ?? DBNull.Value;
                parameters.Add("@sessionId", SqlDbType.UniqueIdentifier).Value = (object?)command.SessionId ?? DBNull.Value;
                parameters.Add("@model", SqlDbType.NVarChar, 100).Value = (object?)command.Model ?? DBNull.Value;
                parameters.Add("@environment", SqlDbType.NVarChar, 100).Value = (object?)command.Environment ?? DBNull.Value;
                parameters.Add("@source", SqlDbType.NVarChar, 100).Value = (object?)command.Source ?? DBNull.Value;
                parameters.Add("@occurredAtUtc", SqlDbType.DateTime2).Value = command.OccurredAtUtc;
                parameters.Add("@registeredAtUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                parameters.Add("@projectResolution", SqlDbType.VarChar, 20).Value = ToCode(projectResolution);
                parameters.AddWithValue("@credentialId", command.CredentialId);

                var itemId = Convert.ToInt64(await insert.ExecuteScalarAsync(cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                return new RegistrationEventOutcome(RegistrationEventStatus.Created, itemId, projectResolution);
            }
            catch (SqlException exception) when (exception.Number is 2601 or 2627)
            {
                var existing = await FindExistingAsync(connection, sqlTransaction, command.TenantId, command.EventId, cancellationToken);
                if (existing is null)
                {
                    throw;
                }

                await transaction.RollbackAsync(cancellationToken);
                return new RegistrationEventOutcome(
                    RegistrationEventStatus.AlreadyExists,
                    existing.Value.ItemId,
                    existing.Value.ProjectResolution);
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<RegistrationEventDto?> GetAsync(
        int tenantId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    r.IdItem, r.IdEvento, r.idTenant, r.IdProyecto, r.sCodigoProyecto,
                    r.sMode, r.sTipoEvento, r.sResumen, r.sDetalles, r.sClaimedActorType,
                    r.nClaimedActorId, r.sAgentName, r.sHost, r.sWorkspace, r.SessionId,
                    r.sModel, r.sEnvironment, r.sSource, r.sExternalProjectKey,
                    COALESCE(r.sProjectName, p.sNombre), r.OccurredAtUtc, r.RegisteredAtUtc,
                    r.sIdentityStatus, r.sProjectResolution, r.IdCredential
                FROM dbo.AgentRegistrationEvent r
                LEFT JOIN dbo.ProProyectosCabecera p
                    ON p.idTenant = r.idTenant AND p.IdProyecto = r.IdProyecto
                WHERE r.idTenant = @tenantId AND r.IdEvento = @eventId;
                """;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.Add("@eventId", SqlDbType.UniqueIdentifier).Value = eventId;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new RegistrationEventDto(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                ReadNullableString(reader, 8),
                ReadNullableString(reader, 9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10),
                ReadNullableString(reader, 11),
                ReadNullableString(reader, 12),
                ReadNullableString(reader, 13),
                reader.IsDBNull(14) ? null : reader.GetGuid(14),
                ReadNullableString(reader, 15),
                ReadNullableString(reader, 16),
                ReadNullableString(reader, 17),
                ReadNullableString(reader, 18),
                ReadNullableString(reader, 19),
                reader.GetDateTime(20),
                reader.GetDateTime(21),
                reader.GetString(22),
                reader.GetString(23),
                reader.GetInt32(24));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> CanCredentialRegisterForProjectAsync(
        int tenantId,
        int credentialId,
        string projectCode,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            return await CanCredentialRegisterForProjectAsync(connection, null, tenantId, credentialId, projectCode, cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM dbo.AgentRegistrationEvent WHERE IdEvento = @eventId;";
            command.Parameters.Add("@eventId", SqlDbType.UniqueIdentifier).Value = eventId;
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> IsIngestionCredentialActiveAsync(
        int tenantId,
        int credentialId,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT 1 FROM dbo.AgentCredential " +
                "WHERE idTenant = @tenantId AND IdCredential = @credentialId AND nActorTipo = 4 " +
                "AND RevokedAtUtc IS NULL AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > SYSUTCDATETIME());";
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@credentialId", credentialId);
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> CanCredentialRegisterForProjectAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        int tenantId,
        int credentialId,
        string projectCode,
        CancellationToken cancellationToken)
    {
        string scopeMode;
        await using (var credential = connection.CreateCommand())
        {
            credential.Transaction = transaction;
            credential.CommandText =
                "SELECT ScopeMode, ExpiresAtUtc FROM dbo.AgentCredential WITH (UPDLOCK, HOLDLOCK) " +
                "WHERE idTenant = @tenantId AND IdCredential = @credentialId AND nActorTipo = 4 AND RevokedAtUtc IS NULL;";
            credential.Parameters.AddWithValue("@tenantId", tenantId);
            credential.Parameters.AddWithValue("@credentialId", credentialId);
            await using var reader = await credential.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return false;
            scopeMode = reader.GetString(0);
            if (!reader.IsDBNull(1) && reader.GetDateTime(1) <= DateTime.UtcNow) return false;
        }

        int? projectId;
        byte? projectStatus;
        await using (var project = connection.CreateCommand())
        {
            project.Transaction = transaction;
            project.CommandText =
                "SELECT TOP (1) IdProyecto, nEstado FROM dbo.ProProyectosCabecera WITH (UPDLOCK, HOLDLOCK) " +
                "WHERE idTenant = @tenantId AND sCodigo = @projectCode;";
            project.Parameters.AddWithValue("@tenantId", tenantId);
            project.Parameters.Add("@projectCode", SqlDbType.VarChar, 50).Value = projectCode;
            await using var reader = await project.ExecuteReaderAsync(cancellationToken);
            projectId = await reader.ReadAsync(cancellationToken) ? reader.GetInt32(0) : null;
            projectStatus = projectId is null ? null : reader.GetByte(1);
        }

        if (projectId is null) return scopeMode == "LEGACY_TENANT_WIDE";
        if (projectStatus != 1) return false;
        if (scopeMode == "LEGACY_TENANT_WIDE") return true;

        await using var assignment = connection.CreateCommand();
        assignment.Transaction = transaction;
        assignment.CommandText =
            "SELECT 1 FROM dbo.AgentCredentialProject WITH (UPDLOCK, HOLDLOCK) " +
            "WHERE idTenant = @tenantId AND IdCredential = @credentialId AND IdProyecto = @projectId AND Activo = 1;";
        assignment.Parameters.AddWithValue("@tenantId", tenantId);
        assignment.Parameters.AddWithValue("@credentialId", credentialId);
        assignment.Parameters.AddWithValue("@projectId", projectId.Value);
        return await assignment.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<int?> FindProjectIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int tenantId,
        string projectCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT TOP (1) IdProyecto FROM dbo.ProProyectosCabecera WHERE idTenant = @tenantId AND sCodigo = @projectCode;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.Add("@projectCode", SqlDbType.VarChar, 50).Value = projectCode;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int projectId ? projectId : null;
    }

    private static async Task<(long ItemId, ProjectRegistrationResolution ProjectResolution)?> FindExistingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int tenantId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT IdItem, sProjectResolution FROM dbo.AgentRegistrationEvent WHERE idTenant = @tenantId AND IdEvento = @eventId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.Add("@eventId", SqlDbType.UniqueIdentifier).Value = eventId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (reader.GetInt64(0), ParseProjectResolution(reader.GetString(1)));
    }

    private static string ToCode(RegistrationEventType eventType) => eventType switch
    {
        RegistrationEventType.Info => "INFO",
        RegistrationEventType.Progress => "PROGRESS",
        RegistrationEventType.Decision => "DECISION",
        RegistrationEventType.Blocker => "BLOCKER",
        RegistrationEventType.Handoff => "HANDOFF",
        RegistrationEventType.Request => "REQUEST",
        RegistrationEventType.Note => "NOTE",
        RegistrationEventType.Error => "ERROR",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown registration event type."),
    };

    private static string ToCode(ProjectRegistrationResolution resolution) => resolution switch
    {
        ProjectRegistrationResolution.Resolved => "RESOLVED",
        ProjectRegistrationResolution.Provisional => "PROVISIONAL",
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unknown project resolution."),
    };

    private static ProjectRegistrationResolution ParseProjectResolution(string value) =>
        value.Equals("PROVISIONAL", StringComparison.OrdinalIgnoreCase)
            ? ProjectRegistrationResolution.Provisional
            : ProjectRegistrationResolution.Resolved;

    private static string? ReadNullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
