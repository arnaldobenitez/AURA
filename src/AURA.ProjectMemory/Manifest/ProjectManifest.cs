namespace AURA.ProjectMemory.Manifest;

/// <summary>
/// In-memory representation of a parsed and schema-valid <c>.aura/project.json</c>.
/// Mirrors docs/aura/project-memory/schemas/project.schema.json field-by-field; this type
/// is a read/write projection of that schema, never a second normative definition of it.
/// </summary>
public sealed record ProjectManifest(
    string SchemaVersion,
    string ProjectMemoryVersion,
    string ProjectId,
    string ProjectName,
    ProjectDocumentation Documentation,
    string? CreatedAt = null,
    string? Status = null,
    IReadOnlyList<string>? Modules = null)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentProjectMemoryVersion = "1.0";
}
