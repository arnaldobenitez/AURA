using AURA.Application.HumanLogin;
using AURA.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AURA.Infrastructure.HumanLogin;

public sealed class SqlHumanLoginRepository(AuraDbContext dbContext) : IHumanLoginRepository
{
    public async Task<HumanLoginRecord?> FindByEmailAsync(int tenantId, string email, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT IdUsuario, sNombre, sPasswordSalt, sPasswordHash, nPasswordIterations
                FROM dbo.Usuarios
                WHERE IdTenant = @tenantId AND LOWER(sEmail) = LOWER(@email)
                    AND sPasswordHash IS NOT NULL;
                """;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@email", email);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new HumanLoginRecord(
                tenantId,
                reader.GetInt32(0),
                reader.GetString(1),
                (byte[])reader[2],
                (byte[])reader[3],
                reader.GetInt32(4));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
