namespace AURA.ProjectMemory.Manifest;

/// <summary>
/// In-memory representation of the local, out-of-project machine profile
/// (conceptually <c>~/.aura/machine.json</c>). Mirrors
/// docs/aura/project-memory/schemas/machine.schema.json. Never part of a project's own files.
/// </summary>
public sealed record MachineProfile(
    string SchemaVersion,
    string MachineId,
    string MachineName,
    string? DisplayName = null)
{
    public const string CurrentSchemaVersion = "1.0";
}
