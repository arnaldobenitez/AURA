using System.Text.Json;
using System.Text.Json.Nodes;

namespace AURA.ProjectMemory.Manifest;

/// <summary>
/// Converts between the local machine profile's raw JSON and <see cref="MachineProfile"/>.
/// Assumes the JSON has already passed schema validation
/// (<see cref="Schemas.ManifestSchemaProvider.MachineSchema"/>).
/// </summary>
public static class MachineProfileSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public static JsonNode? ParseToNode(string json) => JsonNode.Parse(json);

    public static MachineProfile? FromNode(JsonNode node)
    {
        var obj = node.AsObject();

        var schemaVersion = obj["schemaVersion"]?.GetValue<string>();
        var machineId = obj["machineId"]?.GetValue<string>();
        var machineName = obj["machineName"]?.GetValue<string>();

        if (schemaVersion is null || machineId is null || machineName is null)
        {
            return null;
        }

        var displayName = obj["displayName"]?.GetValue<string>();

        return new MachineProfile(schemaVersion, machineId, machineName, displayName);
    }

    public static string ToJson(MachineProfile profile)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = profile.SchemaVersion,
            ["machineId"] = profile.MachineId,
            ["machineName"] = profile.MachineName
        };

        if (profile.DisplayName is not null)
        {
            root["displayName"] = profile.DisplayName;
        }

        return root.ToJsonString(WriteOptions);
    }
}
