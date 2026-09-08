using System.Text.Json;
using System.Text.Json.Nodes;

namespace AURA.ProjectMemory.Manifest;

/// <summary>
/// Converts between <c>.aura/project.json</c> raw JSON and <see cref="ProjectManifest"/>.
/// This class assumes the JSON has already passed schema validation
/// (<see cref="Schemas.ManifestSchemaProvider.ProjectSchema"/>); it does not re-validate shape,
/// it only projects a schema-valid document into a strongly typed object and back.
/// </summary>
public static class ProjectManifestSerializer
{
    private static readonly string[] KnownDocumentationKeys = ["project", "currentState", "history"];

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public static JsonNode? ParseToNode(string json) => JsonNode.Parse(json);

    /// <summary>
    /// Projects an already schema-valid manifest node into a <see cref="ProjectManifest"/>.
    /// Returns null if a required field is unexpectedly absent (should not happen post-validation).
    /// </summary>
    public static ProjectManifest? FromNode(JsonNode node)
    {
        var obj = node.AsObject();

        var schemaVersion = obj["schemaVersion"]?.GetValue<string>();
        var projectMemoryVersion = obj["projectMemoryVersion"]?.GetValue<string>();
        var projectId = obj["projectId"]?.GetValue<string>();
        var projectName = obj["projectName"]?.GetValue<string>();
        var documentationNode = obj["documentation"]?.AsObject();

        if (schemaVersion is null || projectMemoryVersion is null || projectId is null ||
            projectName is null || documentationNode is null)
        {
            return null;
        }

        var project = documentationNode["project"]?.GetValue<string>();
        var currentState = documentationNode["currentState"]?.GetValue<string>();
        var history = documentationNode["history"]?.GetValue<string>();

        if (project is null || currentState is null || history is null)
        {
            return null;
        }

        var extra = new Dictionary<string, string>();
        foreach (var (key, value) in documentationNode)
        {
            if (!KnownDocumentationKeys.Contains(key) && value is not null)
            {
                extra[key] = value.GetValue<string>();
            }
        }

        var createdAt = obj["createdAt"]?.GetValue<string>();
        var status = obj["status"]?.GetValue<string>();
        var modules = obj["modules"]?.AsArray()
            .Select(item => item!.GetValue<string>())
            .ToList();

        return new ProjectManifest(
            schemaVersion,
            projectMemoryVersion,
            projectId,
            projectName,
            new ProjectDocumentation(project, currentState, history, extra),
            createdAt,
            status,
            modules);
    }

    public static string ToJson(ProjectManifest manifest)
    {
        var documentation = new JsonObject
        {
            ["project"] = manifest.Documentation.Project,
            ["currentState"] = manifest.Documentation.CurrentState,
            ["history"] = manifest.Documentation.History
        };

        foreach (var (key, value) in manifest.Documentation.Extra)
        {
            documentation[key] = value;
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = manifest.SchemaVersion,
            ["projectMemoryVersion"] = manifest.ProjectMemoryVersion,
            ["projectId"] = manifest.ProjectId,
            ["projectName"] = manifest.ProjectName,
            ["documentation"] = documentation
        };

        if (manifest.CreatedAt is not null)
        {
            root["createdAt"] = manifest.CreatedAt;
        }

        if (manifest.Status is not null)
        {
            root["status"] = manifest.Status;
        }

        if (manifest.Modules is { Count: > 0 })
        {
            root["modules"] = new JsonArray(manifest.Modules.Select(m => (JsonNode?)m).ToArray());
        }

        return root.ToJsonString(WriteOptions);
    }
}
