using System.Data;
using AURA.Application.Admin;
using AURA.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AURA.Infrastructure.Admin;

public sealed class SqlAdminManagementRepository(AuraDbContext dbContext) : IAdminManagementRepository
{
    public async Task<IReadOnlyList<AdminProjectDto>> ListProjectsAsync(int tenantId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = ProjectSelectSql + " WHERE p.idTenant = @tenantId ORDER BY p.sCodigo, p.IdProyecto;";
            command.Parameters.AddWithValue("@tenantId", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var projects = new List<AdminProjectDto>();
            while (await reader.ReadAsync(cancellationToken)) projects.Add(ReadProject(reader));
            return projects;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminProjectDto?> GetProjectAsync(int tenantId, int projectId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = ProjectSelectSql + " WHERE p.idTenant = @tenantId AND p.IdProyecto = @projectId;";
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@projectId", projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadProject(reader) : null;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminWriteResult> CreateProjectAsync(AdminProjectCreateRequest request, int? actorUserId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            await AcquireProjectLockAsync(connection, sqlTransaction, request.TenantId, cancellationToken);
            if (await FindProjectIdByCodeAsync(connection, sqlTransaction, request.TenantId, request.ProjectCode, cancellationToken) is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.Conflict, Errors: ["Ya existe un projectCode igual en este tenant."]);
            }

            var projectId = await GetNextProjectIdAsync(connection, sqlTransaction, request.TenantId, cancellationToken);
            await using var insert = connection.CreateCommand();
            insert.Transaction = sqlTransaction;
            insert.CommandText =
                "INSERT INTO dbo.ProProyectosCabecera " +
                "(idTenant, IdProyecto, sNombre, sDescripcion, dFecCreacion, sPathTrabajo, nEstado, sCodigo, sRepositorio, " +
                "sExternalProjectKey, dFecModificacion, nUsuarioCreador, nUsuarioModificacion) " +
                "VALUES (@tenantId, @projectId, @name, @description, CAST(SYSUTCDATETIME() AS date), NULL, 1, @code, NULL, @externalKey, SYSUTCDATETIME(), @actor, @actor);";
            insert.Parameters.AddWithValue("@tenantId", request.TenantId);
            insert.Parameters.AddWithValue("@projectId", projectId);
            insert.Parameters.AddWithValue("@name", request.ProjectName.Trim());
            insert.Parameters.Add("@description", SqlDbType.NVarChar, 500).Value = (object?)request.Description?.Trim() ?? DBNull.Value;
            insert.Parameters.Add("@code", SqlDbType.VarChar, 50).Value = request.ProjectCode;
            insert.Parameters.Add("@externalKey", SqlDbType.NVarChar, 200).Value = (object?)request.ExternalProjectKey?.Trim() ?? DBNull.Value;
            insert.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await WriteAuditAsync(connection, sqlTransaction, request.TenantId, "PROJECT_CREATED", "PROJECT", projectId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdminWriteResult(AdminWriteStatus.Succeeded, projectId);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return new AdminWriteResult(AdminWriteStatus.Conflict, Errors: ["Ya existe un projectCode igual en este tenant."]);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminWriteResult> UpdateProjectAsync(int tenantId, int projectId, AdminProjectUpdateRequest request, int? actorUserId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            await AcquireProjectLockAsync(connection, sqlTransaction, tenantId, cancellationToken);
            string? currentCode;
            await using (var current = connection.CreateCommand())
            {
                current.Transaction = sqlTransaction;
                current.CommandText = "SELECT sCodigo FROM dbo.ProProyectosCabecera WITH (UPDLOCK, HOLDLOCK) WHERE idTenant = @tenantId AND IdProyecto = @projectId;";
                current.Parameters.AddWithValue("@tenantId", tenantId);
                current.Parameters.AddWithValue("@projectId", projectId);
                var value = await current.ExecuteScalarAsync(cancellationToken);
                if (value is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new AdminWriteResult(AdminWriteStatus.NotFound);
                }
                currentCode = value is DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (request.ProjectCode is not null && currentCode is not null &&
                !string.Equals(request.ProjectCode, currentCode, StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.Conflict, projectId, ["projectCode es inmutable una vez asignado."]);
            }
            if (request.ProjectCode is not null && currentCode is null &&
                await FindProjectIdByCodeAsync(connection, sqlTransaction, tenantId, request.ProjectCode, cancellationToken) is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.Conflict, projectId, ["Ya existe un projectCode igual en este tenant."]);
            }

            await using var update = connection.CreateCommand();
            update.Transaction = sqlTransaction;
            update.CommandText =
                "UPDATE dbo.ProProyectosCabecera SET sCodigo = COALESCE(sCodigo, @code), sNombre = @name, sDescripcion = @description, " +
                "sExternalProjectKey = @externalKey, dFecModificacion = SYSUTCDATETIME(), nUsuarioModificacion = @actor " +
                "WHERE idTenant = @tenantId AND IdProyecto = @projectId;";
            update.Parameters.AddWithValue("@tenantId", tenantId);
            update.Parameters.AddWithValue("@projectId", projectId);
            update.Parameters.Add("@code", SqlDbType.VarChar, 50).Value = (object?)request.ProjectCode ?? DBNull.Value;
            update.Parameters.AddWithValue("@name", request.ProjectName.Trim());
            update.Parameters.Add("@description", SqlDbType.NVarChar, 500).Value = (object?)request.Description?.Trim() ?? DBNull.Value;
            update.Parameters.Add("@externalKey", SqlDbType.NVarChar, 200).Value = (object?)request.ExternalProjectKey?.Trim() ?? DBNull.Value;
            update.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            await update.ExecuteNonQueryAsync(cancellationToken);
            await WriteAuditAsync(connection, sqlTransaction, tenantId, currentCode is null && request.ProjectCode is not null ? "PROJECT_CODE_ASSIGNED" : "PROJECT_UPDATED", "PROJECT", projectId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdminWriteResult(AdminWriteStatus.Succeeded, projectId);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return new AdminWriteResult(AdminWriteStatus.Conflict, projectId, ["Ya existe un projectCode igual en este tenant."]);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminWriteResult> SetProjectStatusAsync(int tenantId, int projectId, string status, int? actorUserId, CancellationToken cancellationToken)
    {
        var (state, operation) = status switch
        {
            AdminProjectStates.Active => (1, "PROJECT_ACTIVATED"),
            AdminProjectStates.Paused => (2, "PROJECT_PAUSED"),
            AdminProjectStates.Archived => (5, "PROJECT_DEACTIVATED"),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            await using var update = connection.CreateCommand();
            update.Transaction = sqlTransaction;
            update.CommandText = "UPDATE dbo.ProProyectosCabecera SET nEstado = @status, dFecModificacion = SYSUTCDATETIME(), nUsuarioModificacion = @actor WHERE idTenant = @tenantId AND IdProyecto = @projectId;";
            update.Parameters.AddWithValue("@status", state);
            update.Parameters.AddWithValue("@tenantId", tenantId);
            update.Parameters.AddWithValue("@projectId", projectId);
            update.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.NotFound);
            }
            await WriteAuditAsync(connection, sqlTransaction, tenantId, operation, "PROJECT", projectId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdminWriteResult(AdminWriteStatus.Succeeded, projectId);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<AdminAgentCredentialDto>> ListCredentialsAsync(int tenantId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT c.IdCredential, c.sAgentName, c.nActorTipo, CASE WHEN c.nActorTipo = 4 THEN c.ScopeMode ELSE 'NOT_APPLICABLE' END, c.CreatedAtUtc, c.ExpiresAtUtc, c.RevokedAtUtc, " +
                "c.LastUsedAtUtc, c.RotatedFromCredentialId, p.IdProyecto, p.sCodigo, p.sNombre, cp.AssignedAtUtc, cp.RevokedAtUtc " +
                "FROM dbo.AgentCredential c LEFT JOIN dbo.AgentCredentialProject cp ON cp.idTenant = c.idTenant AND cp.IdCredential = c.IdCredential " +
                "LEFT JOIN dbo.ProProyectosCabecera p ON p.idTenant = cp.idTenant AND p.IdProyecto = cp.IdProyecto " +
                "WHERE c.idTenant = @tenantId ORDER BY c.IdCredential, cp.AssignedAtUtc;";
            command.Parameters.AddWithValue("@tenantId", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var credentials = new Dictionary<int, CredentialBuilder>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt32(0);
                if (!credentials.TryGetValue(id, out var builder))
                {
                    var createdAt = reader.GetDateTime(4);
                    DateTime? expiresAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    DateTime? revokedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
                    var actorType = ActorTypeCode(reader.GetByte(2));
                    var scopeMode = reader.GetString(3);
                    var status = revokedAt is not null ? "REVOKED" : expiresAt <= DateTime.UtcNow ? "EXPIRED" : "ACTIVE";
                    builder = new CredentialBuilder(new AdminAgentCredentialDto(
                        tenantId, id, reader.GetString(1), actorType, status, scopeMode, createdAt, expiresAt,
                        revokedAt, reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                        reader.IsDBNull(8) ? null : reader.GetInt32(8), []));
                    credentials.Add(id, builder);
                }

                if (!reader.IsDBNull(9))
                {
                    builder.Projects.Add(new AdminCredentialProjectDto(
                        reader.GetInt32(9), reader.GetString(10), reader.GetString(11),
                        reader.IsDBNull(13), reader.GetDateTime(12), reader.IsDBNull(13) ? null : reader.GetDateTime(13)));
                }
            }
            return credentials.Values.Select(item => item.Dto with { Projects = item.Projects }).ToArray();
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminCredentialCreateResult> CreateCredentialAsync(ScopedCredentialRecord credential, IReadOnlyList<int> projectIds, int? actorUserId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            var projects = await ResolveActiveProjectsAsync(connection, sqlTransaction, credential.TenantId, projectIds, cancellationToken);
            if (projects is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminCredentialCreateResult(AdminWriteStatus.ValidationFailed, Errors: ["Todos los proyectos deben existir, pertenecer al tenant y estar activos."]);
            }

            var id = await GetNextCredentialIdAsync(connection, sqlTransaction, credential.TenantId, cancellationToken);
            await InsertCredentialAsync(connection, sqlTransaction, credential with { CredentialId = id }, "PROJECT_SCOPED", credential.ExpiresAtUtc, actorUserId, cancellationToken);
            await InsertAssignmentsAsync(connection, sqlTransaction, credential.TenantId, id, projects.Select(p => p.ProjectId), actorUserId, cancellationToken);
            await WriteAuditAsync(connection, sqlTransaction, credential.TenantId, "AGENT_CREDENTIAL_CREATED", "AGENT_CREDENTIAL", id, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var projectDtos = projects.Select(p => new AdminCredentialProjectDto(p.ProjectId, p.Code, p.Name, true, credential.CreatedAtUtc, null)).ToArray();
            return new AdminCredentialCreateResult(AdminWriteStatus.Succeeded,
                new AdminCredentialProvisionedDto(credential.TenantId, id, credential.AgentName, credential.ActorType, string.Empty, credential.CreatedAtUtc, credential.ExpiresAtUtc, projectDtos));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminCredentialCreateResult> RotateCredentialAsync(int tenantId, int credentialId, ScopedCredentialRecord replacement, int? actorUserId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            var current = await ReadCredentialForRotationAsync(connection, sqlTransaction, tenantId, credentialId, cancellationToken);
            if (current is null || current.RevokedAtUtc is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminCredentialCreateResult(AdminWriteStatus.NotFound);
            }
            if (current.ActorType == 4 && (current.ScopeMode != "PROJECT_SCOPED" || current.ProjectIds.Count == 0))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminCredentialCreateResult(AdminWriteStatus.ProjectScopeRequired, Errors: ["Asocie al menos un proyecto antes de rotar una credencial INGESTION legacy tenant-wide."]);
            }

            var newId = await GetNextCredentialIdAsync(connection, sqlTransaction, tenantId, cancellationToken);
            var actorType = current.ActorType;
            var record = replacement with
            {
                TenantId = tenantId,
                CredentialId = newId,
                AgentName = current.AgentName,
                ActorType = ActorTypeCode(actorType),
                ExpiresAtUtc = current.ExpiresAtUtc,
                RotatedFromCredentialId = credentialId,
            };
            var scopeMode = actorType == 4 ? "PROJECT_SCOPED" : "NOT_APPLICABLE";
            await InsertCredentialAsync(connection, sqlTransaction, record, scopeMode, record.ExpiresAtUtc, actorUserId, cancellationToken);
            if (current.ProjectIds.Count > 0)
            {
                await InsertAssignmentsAsync(connection, sqlTransaction, tenantId, newId, current.ProjectIds, actorUserId, cancellationToken);
            }

            var now = DateTime.UtcNow;
            await using var revoke = connection.CreateCommand();
            revoke.Transaction = sqlTransaction;
            revoke.CommandText = "UPDATE dbo.AgentCredential SET RevokedAtUtc = @now, UpdatedAtUtc = @now, UpdatedByUserId = @actor WHERE idTenant = @tenantId AND IdCredential = @credentialId AND RevokedAtUtc IS NULL;";
            revoke.Parameters.AddWithValue("@now", now);
            revoke.Parameters.AddWithValue("@tenantId", tenantId);
            revoke.Parameters.AddWithValue("@credentialId", credentialId);
            revoke.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            await revoke.ExecuteNonQueryAsync(cancellationToken);
            await RevokeAssignmentsAsync(connection, sqlTransaction, tenantId, credentialId, actorUserId, now, cancellationToken);
            await WriteAuditAsync(connection, sqlTransaction, tenantId, "AGENT_CREDENTIAL_ROTATED", "AGENT_CREDENTIAL", credentialId, actorUserId, cancellationToken);
            await WriteAuditAsync(connection, sqlTransaction, tenantId, "AGENT_CREDENTIAL_CREATED_BY_ROTATION", "AGENT_CREDENTIAL", newId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var projects = await LoadProjectDtosAsync(connection, null, tenantId, newId, cancellationToken);
            return new AdminCredentialCreateResult(AdminWriteStatus.Succeeded,
                new AdminCredentialProvisionedDto(tenantId, newId, record.AgentName, record.ActorType, string.Empty, record.CreatedAtUtc, record.ExpiresAtUtc, projects));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminWriteResult> RevokeCredentialAsync(int tenantId, int credentialId, int? actorUserId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            var now = DateTime.UtcNow;
            await using var update = connection.CreateCommand();
            update.Transaction = sqlTransaction;
            update.CommandText = "UPDATE dbo.AgentCredential SET RevokedAtUtc = @now, UpdatedAtUtc = @now, UpdatedByUserId = @actor WHERE idTenant = @tenantId AND IdCredential = @credentialId AND RevokedAtUtc IS NULL;";
            update.Parameters.AddWithValue("@now", now);
            update.Parameters.AddWithValue("@tenantId", tenantId);
            update.Parameters.AddWithValue("@credentialId", credentialId);
            update.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.NotFound);
            }
            await RevokeAssignmentsAsync(connection, sqlTransaction, tenantId, credentialId, actorUserId, now, cancellationToken);
            await WriteAuditAsync(connection, sqlTransaction, tenantId, "AGENT_CREDENTIAL_REVOKED", "AGENT_CREDENTIAL", credentialId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdminWriteResult(AdminWriteStatus.Succeeded, credentialId);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminWriteResult> AssignProjectAsync(int tenantId, int credentialId, int projectId, int? actorUserId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            var credential = await ReadCredentialForRotationAsync(connection, sqlTransaction, tenantId, credentialId, cancellationToken);
            if (credential is null || credential.RevokedAtUtc is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.NotFound);
            }
            if (credential.ActorType != 4)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.ValidationFailed, Errors: ["El scope de proyecto solo se aplica a credenciales INGESTION."]);
            }

            var projects = await ResolveActiveProjectsAsync(connection, sqlTransaction, tenantId, [projectId], cancellationToken);
            if (projects is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.ValidationFailed, Errors: ["El proyecto no existe en el tenant o no está activo."]);
            }
            var alreadyAssigned = credential.ProjectIds.Contains(projectId);
            if (alreadyAssigned && credential.ScopeMode == "PROJECT_SCOPED")
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.Conflict, projectId, ["La asignación activa ya existe."]);
            }

            var now = DateTime.UtcNow;
            if (!alreadyAssigned)
            {
                await using var reactivate = connection.CreateCommand();
                reactivate.Transaction = sqlTransaction;
                reactivate.CommandText =
                    "UPDATE dbo.AgentCredentialProject SET Activo = 1, AssignedAtUtc = @now, RevokedAtUtc = NULL, AssignedByUserId = @actor, RevokedByUserId = NULL " +
                    "WHERE idTenant = @tenantId AND IdCredential = @credentialId AND IdProyecto = @projectId AND Activo = 0;";
                reactivate.Parameters.AddWithValue("@now", now);
                reactivate.Parameters.AddWithValue("@tenantId", tenantId);
                reactivate.Parameters.AddWithValue("@credentialId", credentialId);
                reactivate.Parameters.AddWithValue("@projectId", projectId);
                reactivate.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
                if (await reactivate.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    await InsertAssignmentsAsync(connection, sqlTransaction, tenantId, credentialId, [projectId], actorUserId, cancellationToken);
                }
            }

            await using var update = connection.CreateCommand();
            update.Transaction = sqlTransaction;
            update.CommandText = "UPDATE dbo.AgentCredential SET ScopeMode = 'PROJECT_SCOPED', UpdatedAtUtc = @now, UpdatedByUserId = @actor WHERE idTenant = @tenantId AND IdCredential = @credentialId;";
            update.Parameters.AddWithValue("@now", now);
            update.Parameters.AddWithValue("@tenantId", tenantId);
            update.Parameters.AddWithValue("@credentialId", credentialId);
            update.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            await update.ExecuteNonQueryAsync(cancellationToken);
            await WriteAuditAsync(connection, sqlTransaction, tenantId, alreadyAssigned ? "AGENT_CREDENTIAL_SCOPE_HARDENED" : "AGENT_CREDENTIAL_PROJECT_ASSIGNED", "AGENT_CREDENTIAL", credentialId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdminWriteResult(AdminWriteStatus.Succeeded, projectId);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return new AdminWriteResult(AdminWriteStatus.Conflict, projectId, ["La asignación activa ya existe."]);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AdminWriteResult> RevokeProjectAssignmentAsync(int tenantId, int credentialId, int projectId, int? actorUserId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            var now = DateTime.UtcNow;
            await using var update = connection.CreateCommand();
            update.Transaction = sqlTransaction;
            update.CommandText = "UPDATE dbo.AgentCredentialProject SET Activo = 0, RevokedAtUtc = @now, RevokedByUserId = @actor WHERE idTenant = @tenantId AND IdCredential = @credentialId AND IdProyecto = @projectId AND Activo = 1;";
            update.Parameters.AddWithValue("@now", now);
            update.Parameters.AddWithValue("@tenantId", tenantId);
            update.Parameters.AddWithValue("@credentialId", credentialId);
            update.Parameters.AddWithValue("@projectId", projectId);
            update.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AdminWriteResult(AdminWriteStatus.NotFound);
            }
            await WriteAuditAsync(connection, sqlTransaction, tenantId, "AGENT_CREDENTIAL_PROJECT_REVOKED", "AGENT_CREDENTIAL", credentialId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdminWriteResult(AdminWriteStatus.Succeeded, projectId);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<AdminCredentialProjectDto>> ListCredentialProjectsAsync(int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            return await LoadProjectDtosAsync(connection, null, tenantId, credentialId, cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private const string ProjectSelectSql =
        "SELECT p.idTenant, p.IdProyecto, p.sCodigo, p.sNombre, p.sDescripcion, p.sExternalProjectKey, p.nEstado, " +
        "CAST(p.dFecCreacion AS datetime2), p.dFecModificacion, p.nUsuarioCreador, p.nUsuarioModificacion " +
        "FROM dbo.ProProyectosCabecera p";

    private static AdminProjectDto ReadProject(SqlDataReader reader)
    {
        var state = reader.GetByte(6);
        return new AdminProjectDto(
            reader.GetInt32(0), reader.GetInt32(1), ReadString(reader, 2), reader.GetString(3),
            ReadString(reader, 4), ReadString(reader, 5), ProjectStatusCode(state), state == 1,
            reader.IsDBNull(7) ? null : reader.GetDateTime(7), reader.IsDBNull(8) ? null : reader.GetDateTime(8),
            reader.IsDBNull(9) ? null : reader.GetInt32(9), reader.IsDBNull(10) ? null : reader.GetInt32(10));
    }

    private static string ProjectStatusCode(byte state) => state switch
    {
        0 => "DRAFT", 1 => "ACTIVE", 2 => "PAUSED", 3 => "BLOCKED", 4 => "COMPLETED", 5 => "ARCHIVED", _ => "UNKNOWN",
    };

    private static string ActorTypeCode(byte actorType) => actorType switch
    {
        1 => "HUMAN", 2 => "AI_ANALYST", 3 => "CODER", 4 => "INGESTION", _ => "UNASSIGNED",
    };

    private static string? ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static async Task<int?> FindProjectIdByCodeAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, string projectCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TOP (1) IdProyecto FROM dbo.ProProyectosCabecera WITH (UPDLOCK, HOLDLOCK) WHERE idTenant = @tenantId AND sCodigo = @code;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.Add("@code", SqlDbType.VarChar, 50).Value = projectCode;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int id ? id : null;
    }

    private static async Task<int> GetNextProjectIdAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ISNULL(MAX(IdProyecto), 0) + 1 FROM dbo.ProProyectosCabecera WITH (UPDLOCK, HOLDLOCK) WHERE idTenant = @tenantId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task AcquireProjectLockAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;";
        command.Parameters.AddWithValue("@resource", $"ProProyectosCabecera:{tenantId}");
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        command.Parameters.Add(result);
        await command.ExecuteNonQueryAsync(cancellationToken);
        if ((int)(result.Value ?? -999) < 0) throw new InvalidOperationException("No se pudo bloquear la secuencia de proyectos.");
    }

    private static async Task<int> GetNextCredentialIdAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ISNULL(MAX(IdCredential), 0) + 1 FROM dbo.AgentCredential WITH (UPDLOCK, HOLDLOCK) WHERE idTenant = @tenantId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<List<ProjectRow>?> ResolveActiveProjectsAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, IReadOnlyList<int> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0 || projectIds.Distinct().Count() != projectIds.Count) return null;
        var parameters = projectIds.Select((_, index) => $"@p{index}").ToArray();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT IdProyecto, sCodigo, sNombre FROM dbo.ProProyectosCabecera WITH (UPDLOCK, HOLDLOCK) WHERE idTenant = @tenantId AND nEstado = 1 AND sCodigo IS NOT NULL AND IdProyecto IN ({string.Join(",", parameters)});";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        for (var i = 0; i < projectIds.Count; i++) command.Parameters.AddWithValue(parameters[i], projectIds[i]);
        var rows = new List<ProjectRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new ProjectRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return rows.Count == projectIds.Count ? rows : null;
    }

    private static async Task InsertCredentialAsync(SqlConnection connection, SqlTransaction transaction, ScopedCredentialRecord credential, string scopeMode, DateTime? expiresAtUtc, int? actorUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO dbo.AgentCredential (idTenant, IdCredential, nActorTipo, sAgentName, LinkedActorId, SecretSalt, SecretHash, SecretIterations, " +
            "CreatedAtUtc, RevokedAtUtc, LastUsedAtUtc, RotatedFromCredentialId, ScopeMode, ExpiresAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId) " +
            "VALUES (@tenantId, @id, @actorType, @agentName, NULL, @salt, @hash, @iterations, @created, NULL, NULL, @rotatedFrom, @scope, @expires, @actor, @created, @actor);";
        command.Parameters.AddWithValue("@tenantId", credential.TenantId);
        command.Parameters.AddWithValue("@id", credential.CredentialId);
        command.Parameters.Add("@actorType", SqlDbType.TinyInt).Value = ActorTypeNumber(credential.ActorType);
        command.Parameters.Add("@agentName", SqlDbType.NVarChar, 100).Value = credential.AgentName;
        command.Parameters.Add("@salt", SqlDbType.VarBinary, 16).Value = credential.SecretSalt;
        command.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = credential.SecretHash;
        command.Parameters.AddWithValue("@iterations", credential.SecretIterations);
        command.Parameters.AddWithValue("@created", credential.CreatedAtUtc);
        command.Parameters.Add("@rotatedFrom", SqlDbType.Int).Value = (object?)credential.RotatedFromCredentialId ?? DBNull.Value;
        command.Parameters.Add("@scope", SqlDbType.VarChar, 30).Value = scopeMode;
        command.Parameters.Add("@expires", SqlDbType.DateTime2).Value = (object?)expiresAtUtc ?? DBNull.Value;
        command.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static byte ActorTypeNumber(string value) => value.ToUpperInvariant() switch
    {
        "HUMAN" => 1, "AI_ANALYST" => 2, "CODER" => 3, "INGESTION" => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static async Task InsertAssignmentsAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, int credentialId, IEnumerable<int> projectIds, int? actorUserId, CancellationToken cancellationToken)
    {
        foreach (var projectId in projectIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO dbo.AgentCredentialProject (idTenant, IdCredential, IdProyecto, Activo, AssignedAtUtc, RevokedAtUtc, AssignedByUserId, RevokedByUserId) VALUES (@tenantId, @credentialId, @projectId, 1, SYSUTCDATETIME(), NULL, @actor, NULL);";
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@credentialId", credentialId);
            command.Parameters.AddWithValue("@projectId", projectId);
            command.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task RevokeAssignmentsAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, int credentialId, int? actorUserId, DateTime now, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE dbo.AgentCredentialProject SET Activo = 0, RevokedAtUtc = @now, RevokedByUserId = @actor WHERE idTenant = @tenantId AND IdCredential = @credentialId AND Activo = 1;";
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@credentialId", credentialId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteAuditAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, string operation, string targetType, int targetId, int? actorUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO dbo.AuraAdminAudit (idTenant, OperationCode, TargetType, TargetId, ActorUserId, OccurredAtUtc) VALUES (@tenantId, @operation, @targetType, @targetId, @actor, SYSUTCDATETIME());";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.Add("@operation", SqlDbType.VarChar, 60).Value = operation;
        command.Parameters.Add("@targetType", SqlDbType.VarChar, 40).Value = targetType;
        command.Parameters.AddWithValue("@targetId", targetId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.Add("@actor", SqlDbType.Int).Value = (object?)actorUserId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<CredentialRow?> ReadCredentialForRotationAsync(SqlConnection connection, SqlTransaction transaction, int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT nActorTipo, sAgentName, ScopeMode, RevokedAtUtc, ExpiresAtUtc FROM dbo.AgentCredential WITH (UPDLOCK, HOLDLOCK) WHERE idTenant = @tenantId AND IdCredential = @credentialId;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@credentialId", credentialId);
        string scopeMode;
        string name;
        byte actorType;
        DateTime? revoked;
        DateTime? expires;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            actorType = reader.GetByte(0);
            name = reader.GetString(1);
            scopeMode = reader.GetString(2);
            revoked = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            expires = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
        }

        var projectIds = new List<int>();
        await using var projects = connection.CreateCommand();
        projects.Transaction = transaction;
        projects.CommandText = "SELECT IdProyecto FROM dbo.AgentCredentialProject WHERE idTenant = @tenantId AND IdCredential = @credentialId AND Activo = 1 ORDER BY IdProyecto;";
        projects.Parameters.AddWithValue("@tenantId", tenantId);
        projects.Parameters.AddWithValue("@credentialId", credentialId);
        await using var projectReader = await projects.ExecuteReaderAsync(cancellationToken);
        while (await projectReader.ReadAsync(cancellationToken)) projectIds.Add(projectReader.GetInt32(0));
        return new CredentialRow(actorType, name, scopeMode, revoked, expires, projectIds);
    }

    private static async Task<IReadOnlyList<AdminCredentialProjectDto>> LoadProjectDtosAsync(SqlConnection connection, SqlTransaction? transaction, int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT cp.IdProyecto, p.sCodigo, p.sNombre, p.nEstado, cp.AssignedAtUtc, cp.RevokedAtUtc " +
            "FROM dbo.AgentCredentialProject cp LEFT JOIN dbo.ProProyectosCabecera p ON p.idTenant = cp.idTenant AND p.IdProyecto = cp.IdProyecto " +
            "WHERE cp.idTenant = @tenantId AND cp.IdCredential = @credentialId ORDER BY cp.AssignedAtUtc;";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@credentialId", credentialId);
        var list = new List<AdminCredentialProjectDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new AdminCredentialProjectDto(reader.GetInt32(0), ReadString(reader, 1) ?? "", ReadString(reader, 2) ?? "[proyecto inactivo/eliminado]",
                reader.IsDBNull(5), reader.GetDateTime(4), reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
        }
        return list;
    }

    private sealed class CredentialBuilder(AdminAgentCredentialDto dto)
    {
        public AdminAgentCredentialDto Dto { get; } = dto;
        public List<AdminCredentialProjectDto> Projects { get; } = [];
    }

    private sealed record ProjectRow(int ProjectId, string Code, string Name);
    private sealed record CredentialRow(byte ActorType, string AgentName, string ScopeMode, DateTime? RevokedAtUtc, DateTime? ExpiresAtUtc, IReadOnlyList<int> ProjectIds);
}
