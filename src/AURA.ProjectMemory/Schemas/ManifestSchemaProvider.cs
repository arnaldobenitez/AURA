using System.Reflection;
using Json.Schema;

namespace AURA.ProjectMemory.Schemas;

/// <summary>
/// Loads the normative JSON Schemas (docs/aura/project-memory/schemas/*.json) from embedded
/// resources and exposes them as compiled <see cref="JsonSchema"/> instances. The schema files
/// are the only definition of the manifest/machine-profile contracts; this class must never
/// re-encode their rules (required fields, UUID v4 pattern, relative-path pattern, etc.) by hand.
/// </summary>
public static class ManifestSchemaProvider
{
    private static readonly Assembly ResourceAssembly = typeof(ManifestSchemaProvider).Assembly;

    private static readonly Lazy<JsonSchema> LazyProjectSchema = new(() => LoadSchema("project.schema.json"));
    private static readonly Lazy<JsonSchema> LazyMachineSchema = new(() => LoadSchema("machine.schema.json"));

    public static JsonSchema ProjectSchema => LazyProjectSchema.Value;

    public static JsonSchema MachineSchema => LazyMachineSchema.Value;

    private static JsonSchema LoadSchema(string fileName)
    {
        var resourceName = ResourceAssembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("." + fileName, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"AURA Project Memory schema '{fileName}' is missing from the embedded resources. " +
                "This indicates a packaging defect in AURA.ProjectMemory, not a user error.");
        }

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open embedded resource stream for '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }
}
