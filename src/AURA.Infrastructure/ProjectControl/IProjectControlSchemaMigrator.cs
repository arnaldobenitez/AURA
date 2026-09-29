namespace AURA.Infrastructure.ProjectControl;

public interface IProjectControlSchemaMigrator
{
    Task ApplyAsync(CancellationToken cancellationToken);

    Task ApplySchemaOnlyAsync(CancellationToken cancellationToken);
}
