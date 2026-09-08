using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Initialization;

public class MachineProfileInitializerTests
{
    [Fact]
    public void Init_CreatesValidProfileWithUuidV4MachineId()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");

        var result = MachineProfileInitializer.Init(path);

        Assert.True(result.Success);
        Assert.NotNull(result.MachineId);
        Assert.NotNull(result.MachineName);
        Assert.True(File.Exists(path));

        var node = MachineProfileSerializer.ParseToNode(File.ReadAllText(path))!;
        var profile = MachineProfileSerializer.FromNode(node)!;
        Assert.Equal(result.MachineId, profile.MachineId);
        Assert.Equal(result.MachineName, profile.MachineName);
        var guid = Guid.Parse(profile.MachineId);
        Assert.Equal(4, (guid.ToByteArray()[7] >> 4) & 0x0F);
        Assert.False(string.IsNullOrWhiteSpace(profile.MachineName));
    }

    [Fact]
    public void Init_DoesNotOverwriteAnExistingProfile()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");
        File.WriteAllText(path, """{ "schemaVersion": "1.0", "machineId": "existing", "machineName": "keep-me" }""");

        var result = MachineProfileInitializer.Init(path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.AlreadyInitialized);
        Assert.Contains("keep-me", File.ReadAllText(path));
    }

    [Fact]
    public void Init_NeverWritesLogicalUserOrOsUserOrProjectPaths()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");

        MachineProfileInitializer.Init(path);

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("logicalUser", json);
        Assert.DoesNotContain("osUser", json);
        Assert.DoesNotContain("localProjectPath", json);
    }
}
