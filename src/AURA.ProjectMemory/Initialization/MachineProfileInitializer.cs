using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Validation;

// MachineProfileValidator.DefaultPath() is the single source for the conventional
// ~/.aura/machine.json location; ProjectMemoryValidator no longer duplicates it.

namespace AURA.ProjectMemory.Initialization;

/// <summary>
/// Minimal, optional initializer for the local machine profile (AURA_PROJECT_MEMORY v1.0 §5.2).
/// This is deliberately small: it only ever writes <c>machineId</c>/<c>machineName</c>, never
/// <c>logicalUser</c>/<c>osUser</c>/project paths, and never overwrites an existing profile.
/// </summary>
public static class MachineProfileInitializer
{
    public static MachineInitResult Init(string? path = null)
    {
        var profilePath = path ?? MachineProfileValidator.DefaultPath();

        if (File.Exists(profilePath))
        {
            return new MachineInitResult(false, null, null, [
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.AlreadyInitialized,
                    "A machine profile already exists. It is never overwritten automatically.",
                    profilePath)
            ]);
        }

        var machineId = Guid.NewGuid().ToString();
        var machineName = Environment.MachineName;

        var profile = new MachineProfile(MachineProfile.CurrentSchemaVersion, machineId, machineName);
        var json = MachineProfileSerializer.ToJson(profile);

        try
        {
            var directory = Path.GetDirectoryName(profilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(profilePath, json);
        }
        catch (IOException ex)
        {
            return new MachineInitResult(false, null, null, [
                new Diagnostic(DiagnosticSeverity.Error, DiagnosticCode.MachineProfileInvalid,
                    $"Could not write the machine profile: {ex.Message}")
            ]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new MachineInitResult(false, null, null, [
                new Diagnostic(DiagnosticSeverity.Error, DiagnosticCode.MachineProfileInvalid,
                    $"Could not write the machine profile due to insufficient permissions: {ex.Message}")
            ]);
        }

        return new MachineInitResult(true, machineId, machineName, []);
    }
}
