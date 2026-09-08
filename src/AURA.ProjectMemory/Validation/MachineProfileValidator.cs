using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Schemas;

namespace AURA.ProjectMemory.Validation;

/// <summary>
/// Validates the local, out-of-project machine profile against machine.schema.json. Used both
/// standalone (<c>aura machine validate</c>) and as a non-blocking check embedded in
/// <see cref="ProjectMemoryValidator"/> and <see cref="Session.SessionManager"/> - a single
/// implementation, never two copies of the same rule.
/// </summary>
public static class MachineProfileValidator
{
    private const string DefaultMachineProfileFileName = "machine.json";

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aura", DefaultMachineProfileFileName);

    public static MachineValidationResult Validate(string? path = null)
    {
        var resolvedPath = path ?? DefaultPath();

        if (!File.Exists(resolvedPath))
        {
            return new MachineValidationResult(false, resolvedPath, null, null, [
                new Diagnostic(
                    DiagnosticSeverity.Info,
                    DiagnosticCode.MachineProfileAbsent,
                    "No local machine profile is configured. It is optional and never required for Core compliance.",
                    resolvedPath)
            ]);
        }

        string rawJson;
        try
        {
            rawJson = File.ReadAllText(resolvedPath);
        }
        catch (IOException ex)
        {
            return Invalid(resolvedPath, $"Could not read the machine profile: {ex.Message}");
        }

        System.Text.Json.Nodes.JsonNode? node;
        try
        {
            node = MachineProfileSerializer.ParseToNode(rawJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Invalid(resolvedPath, $"The machine profile is not valid JSON: {ex.Message}");
        }

        if (node is null)
        {
            return Invalid(resolvedPath, "The machine profile file is empty.");
        }

        var element = System.Text.Json.JsonSerializer.SerializeToElement(node);
        var evaluation = ManifestSchemaProvider.MachineSchema.Evaluate(element,
            new Json.Schema.EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.List });

        if (!evaluation.IsValid)
        {
            return Invalid(resolvedPath, "The machine profile does not conform to machine.schema.json.");
        }

        var profile = MachineProfileSerializer.FromNode(node);
        return new MachineValidationResult(true, resolvedPath, profile?.MachineId, profile?.MachineName, []);
    }

    private static MachineValidationResult Invalid(string resolvedPath, string message) =>
        new(false, resolvedPath, null, null, [
            new Diagnostic(DiagnosticSeverity.Warning, DiagnosticCode.MachineProfileInvalid, message, resolvedPath)
        ]);
}
