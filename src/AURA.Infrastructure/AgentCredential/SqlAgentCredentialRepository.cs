using System.Data;
using AURA.Application.AgentCredential;
using AURA.Domain.ProjectControl;
using AURA.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AURA.Infrastructure.AgentCredential;

public sealed class SqlAgentCredentialRepository(AuraDbContext dbContext) : IAgentCredentialRepository
{
    public async Task<int> CreateAsync(NewAgentCredentialRecord record, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();

            await using var nextIdCommand = connection.CreateCommand();
            nextIdCommand.Transaction = sqlTransaction;
            nextIdCommand.CommandText =
                "SELECT ISNULL(MAX(IdCredential), 0) FROM dbo.AgentCredential WITH (UPDLOCK, HOLDLOCK) WHERE idTenant = @tenantId;";
            nextIdCommand.Parameters.AddWithValue("@tenantId", record.TenantId);
            var nextId = Convert.ToInt32(await nextIdCommand.ExecuteScalarAsync(cancellationToken)) + 1;

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = sqlTransaction;
            insertCommand.CommandText =
                """
                INSERT INTO dbo.AgentCredential
                    (idTenant, IdCredential, nActorTipo, sAgentName, LinkedActorId,
                     SecretSalt, SecretHash, SecretIterations, CreatedAtUtc, RevokedAtUtc, LastUsedAtUtc, RotatedFromCredentialId)
                VALUES
                    (@tenantId, @credentialId, @actorType, @agentName, @linkedActorId,
                     @secretSalt, @secretHash, @secretIterations, @createdAtUtc, NULL, NULL, @rotatedFromCredentialId);
                """;
            var parameters = insertCommand.Parameters;
            parameters.AddWithValue("@tenantId", record.TenantId);
            parameters.AddWithValue("@credentialId", nextId);
            parameters.Add("@actorType", SqlDbType.TinyInt).Value = (byte)record.ActorType;
            parameters.AddWithValue("@agentName", record.AgentName);
            parameters.Add("@linkedActorId", SqlDbType.Int).Value = (object?)record.LinkedActorId ?? DBNull.Value;
            parameters.Add("@secretSalt", SqlDbType.VarBinary).Value = record.SecretSalt;
            parameters.Add("@secretHash", SqlDbType.VarBinary).Value = record.SecretHash;
            parameters.AddWithValue("@secretIterations", record.SecretIterations);
            parameters.AddWithValue("@createdAtUtc", record.CreatedAtUtc);
            parameters.Add("@rotatedFromCredentialId", SqlDbType.Int).Value = (object?)record.RotatedFromCredentialId ?? DBNull.Value;

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return nextId;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<AgentCredentialAuthRecord?> FindForAuthenticationAsync(int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT nActorTipo, sAgentName, LinkedActorId, SecretSalt, SecretHash, SecretIterations, RevokedAtUtc
                FROM dbo.AgentCredential
                WHERE idTenant = @tenantId AND IdCredential = @credentialId;
                """;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@credentialId", credentialId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new AgentCredentialAuthRecord(
                tenantId,
                credentialId,
                (ProjectActorType)reader.GetByte(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                (byte[])reader[3],
                (byte[])reader[4],
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetDateTime(6));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task TouchLastUsedAsync(int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE dbo.AgentCredential SET LastUsedAtUtc = @now WHERE idTenant = @tenantId AND IdCredential = @credentialId;";
            command.Parameters.AddWithValue("@now", DateTime.UtcNow);
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@credentialId", credentialId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<RevokeAgentCredentialStatus> RevokeAsync(int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();

            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText =
                "SELECT RevokedAtUtc FROM dbo.AgentCredential WHERE idTenant = @tenantId AND IdCredential = @credentialId;";
            checkCommand.Parameters.AddWithValue("@tenantId", tenantId);
            checkCommand.Parameters.AddWithValue("@credentialId", credentialId);

            object? existingRevokedAt;
            await using (var reader = await checkCommand.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return RevokeAgentCredentialStatus.NotFound;
                }

                existingRevokedAt = reader.IsDBNull(0) ? null : reader.GetDateTime(0);
            }

            if (existingRevokedAt is not null)
            {
                return RevokeAgentCredentialStatus.AlreadyRevoked;
            }

            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText =
                "UPDATE dbo.AgentCredential SET RevokedAtUtc = @now WHERE idTenant = @tenantId AND IdCredential = @credentialId;";
            updateCommand.Parameters.AddWithValue("@now", DateTime.UtcNow);
            updateCommand.Parameters.AddWithValue("@tenantId", tenantId);
            updateCommand.Parameters.AddWithValue("@credentialId", credentialId);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);

            return RevokeAgentCredentialStatus.Revoked;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<AgentCredentialSummaryDto>> ListAsync(int tenantId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT IdCredential, nActorTipo, sAgentName, LinkedActorId, CreatedAtUtc, RevokedAtUtc, LastUsedAtUtc, RotatedFromCredentialId
                FROM dbo.AgentCredential
                WHERE idTenant = @tenantId
                ORDER BY IdCredential ASC;
                """;
            command.Parameters.AddWithValue("@tenantId", tenantId);

            var results = new List<AgentCredentialSummaryDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new AgentCredentialSummaryDto(
                    reader.GetInt32(0),
                    AURA.Application.ProjectControl.ProjectControlCodes.ToCode((ProjectActorType)reader.GetByte(1)),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.GetDateTime(4),
                    reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    reader.IsDBNull(7) ? null : reader.GetInt32(7)));
            }

            return results;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
