using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Tests.TestSupport;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Tests.Validation;

public class MachineProfileRenameTests
{
    [Fact]
    public void RenameMachineName_DoesNotChangeMachineId()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");

        var init = MachineProfileInitializer.Init(path);
        Assert.True(init.Success);
        var originalMachineId = init.MachineId;

        // Simulate the user editing only machineName (e.g. after a hostname change).
        var json = File.ReadAllText(path);
        json = json.Replace($"\"machineName\": \"{init.MachineName}\"", "\"machineName\": \"NEW-NAME\"");
        File.WriteAllText(path, json);

        var validation = MachineProfileValidator.Validate(path);

        Assert.True(validation.IsValid);
        Assert.Equal(originalMachineId, validation.MachineId);
    }
}
