using System.Text.Json;
using AURA.ProjectMemory.Schemas;

namespace AURA.ProjectMemory.Tests.Schemas;

/// <summary>
/// Automates, against the real embedded JSON Schemas, the same valid/invalid matrix that was
/// manually exercised with ajv-cli during Etapa 3 (see AURA-PROTOCOL-V2-01-style validation
/// notes in the Etapa 3 report). This is what makes schema conformance a regression-tested
/// property of the tool instead of a one-off manual check.
/// </summary>
public class ManifestSchemaProviderTests
{
    private static bool IsValid(Json.Schema.JsonSchema schema, string json) =>
        schema.Evaluate(JsonSerializer.SerializeToElement(JsonNode(json))).IsValid;

    private static System.Text.Json.Nodes.JsonNode JsonNode(string json) =>
        System.Text.Json.Nodes.JsonNode.Parse(json)!;

    [Fact]
    public void ProjectSchema_ValidManifest_Passes()
    {
        const string json = """
            {
              "schemaVersion": "1.0",
              "projectMemoryVersion": "1.0",
              "projectId": "550e8400-e29b-41d4-a716-446655440000",
              "projectName": "Example",
              "documentation": {
                "project": "docs/00-PROYECTO.md",
                "currentState": "docs/01-ESTADO-ACTUAL.md",
                "history": "docs/historial"
              }
            }
            """;

        Assert.True(IsValid(ManifestSchemaProvider.ProjectSchema, json));
    }

    [Fact]
    public void ProjectSchema_ValidManifestWithOptionalModules_Passes()
    {
        const string json = """
            {
              "schemaVersion": "1.0",
              "projectMemoryVersion": "1.0",
              "projectId": "550e8400-e29b-41d4-a716-446655440000",
              "projectName": "Example",
              "documentation": {
                "project": "docs/00-PROYECTO.md",
                "currentState": "docs/01-ESTADO-ACTUAL.md",
                "history": "docs/historial",
                "backlog": "docs/03-BACKLOG.md"
              },
              "modules": ["BACKLOG"]
            }
            """;

        Assert.True(IsValid(ManifestSchemaProvider.ProjectSchema, json));
    }

    [Theory]
    [InlineData("""{"schemaVersion":"1.0","projectMemoryVersion":"1.0","projectId":"not-a-uuid","projectName":"E","documentation":{"project":"docs/00-PROYECTO.md","currentState":"docs/01-ESTADO-ACTUAL.md","history":"docs/historial"}}""")]
    [InlineData("""{"schemaVersion":"1.0","projectMemoryVersion":"1.0","projectId":"550e8400-e29b-41d4-a716-446655440000","projectName":"E","documentation":{"project":"C:/Project/docs/00-PROYECTO.md","currentState":"docs/01-ESTADO-ACTUAL.md","history":"docs/historial"}}""")]
    [InlineData("""{"schemaVersion":"1.0","projectMemoryVersion":"1.0","projectId":"550e8400-e29b-41d4-a716-446655440000","projectName":"E","documentation":{"project":"/abs/docs/00-PROYECTO.md","currentState":"docs/01-ESTADO-ACTUAL.md","history":"docs/historial"}}""")]
    [InlineData("""{"schemaVersion":"1.0","projectMemoryVersion":"1.0","projectId":"550e8400-e29b-41d4-a716-446655440000","projectName":"E","documentation":{"project":"docs/00-PROYECTO.md","currentState":"docs/../01-ESTADO-ACTUAL.md","history":"docs/historial"}}""")]
    [InlineData("""{"schemaVersion":"1.0","projectMemoryVersion":"1.0","projectId":"550e8400-e29b-41d4-a716-446655440000","projectName":"E","documentation":{"project":"docs/00-PROYECTO.md","history":"docs/historial"}}""")]
    [InlineData("""{"schemaVersion":"1.0","projectMemoryVersion":"1.0","projectId":"550e8400-e29b-41d4-a716-446655440000","projectName":"E","documentation":{"project":"docs/00-PROYECTO.md","currentState":"docs/01-ESTADO-ACTUAL.md"}}""")]
    [InlineData("""{"schemaVersion":"2.0","projectMemoryVersion":"1.0","projectId":"550e8400-e29b-41d4-a716-446655440000","projectName":"E","documentation":{"project":"docs/00-PROYECTO.md","currentState":"docs/01-ESTADO-ACTUAL.md","history":"docs/historial"}}""")]
    public void ProjectSchema_InvalidManifest_Fails(string json)
    {
        Assert.False(IsValid(ManifestSchemaProvider.ProjectSchema, json));
    }

    [Fact]
    public void MachineSchema_ValidProfile_Passes()
    {
        const string json = """{"schemaVersion":"1.0","machineId":"6ba7b810-9dad-41d1-80b4-00c04fd430c8","machineName":"Example Machine"}""";

        Assert.True(IsValid(ManifestSchemaProvider.MachineSchema, json));
    }

    [Theory]
    [InlineData("""{"schemaVersion":"1.0","machineId":"not-a-uuid","machineName":"Example"}""")]
    [InlineData("""{"schemaVersion":"1.0","machineId":"6ba7b810-9dad-41d1-80b4-00c04fd430c8"}""")]
    [InlineData("""{"schemaVersion":"2.0","machineId":"6ba7b810-9dad-41d1-80b4-00c04fd430c8","machineName":"Example"}""")]
    public void MachineSchema_InvalidProfile_Fails(string json)
    {
        Assert.False(IsValid(ManifestSchemaProvider.MachineSchema, json));
    }
}
