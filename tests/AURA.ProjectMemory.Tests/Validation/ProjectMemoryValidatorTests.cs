using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Tests.TestSupport;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Tests.Validation;

public class ProjectMemoryValidatorTests
{
    private static string NonExistentMachineProfilePath(TempDirectory dir) =>
        dir.Combine("unused-machine-profile", "machine.json");

    [Fact]
    public void Validate_FreshlyInitializedProject_IsValid()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingAuraMd_IsError()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        File.Delete(dir.Combine("AURA.md"));

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, d => d.Code == DiagnosticCode.MissingCoreDocument && d.Path == "AURA.md");
    }

    [Fact]
    public void Validate_MissingManifest_IsError()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        File.Delete(dir.Combine(".aura", "project.json"));

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, d => d.Code == DiagnosticCode.ProjectRootNotFound);
    }

    [Fact]
    public void Validate_PartialInitializationWithoutManifest_IsInvalidAndDoesNotInventRoot()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.Combine("AURA.md"), "partial");

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.False(result.IsValid);
        Assert.Null(result.ProjectRoot);
        Assert.Contains(result.Errors, d => d.Code == DiagnosticCode.ProjectRootNotFound);
    }

    [Fact]
    public void Validate_InvalidProjectIdInManifest_IsError()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        var manifestPath = dir.Combine(".aura", "project.json");
        var json = File.ReadAllText(manifestPath).Replace(
            System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!["projectId"]!.GetValue<string>(),
            "not-a-valid-uuid");
        File.WriteAllText(manifestPath, json);

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, d => d.Code == DiagnosticCode.InvalidProjectManifest);
    }

    [Theory]
    [InlineData(@"C:/Absolute/docs/00-PROYECTO.md")]
    [InlineData("/home/user/docs/00-PROYECTO.md")]
    [InlineData("docs/../00-PROYECTO.md")]
    public void Validate_AbsoluteOrTraversingDocumentationPath_IsError(string maliciousPath)
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        var manifestPath = dir.Combine(".aura", "project.json");
        var json = File.ReadAllText(manifestPath).Replace("docs/00-PROYECTO.md", maliciousPath);
        File.WriteAllText(manifestPath, json);

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, d =>
            d.Code == DiagnosticCode.InvalidProjectManifest || d.Code == DiagnosticCode.AbsoluteDocumentationPath);
    }

    [Fact]
    public void Validate_MissingCurrentStateDocument_IsError()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        File.Delete(dir.Combine("docs", "01-ESTADO-ACTUAL.md"));

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, d => d.Code == DiagnosticCode.MissingCoreDocument);
    }

    [Fact]
    public void Validate_MissingHistoryDirectory_IsError()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        Directory.Delete(dir.Combine("docs", "historial"));

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, d => d.Code == DiagnosticCode.MissingCoreDocument);
    }

    [Fact]
    public void Validate_EmptyHistoryDirectory_IsValid()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.True(result.IsValid);
        Assert.True(Directory.Exists(dir.Combine("docs", "historial")));
        Assert.Empty(Directory.GetFileSystemEntries(dir.Combine("docs", "historial")));
    }

    [Fact]
    public void Validate_MissingOptionalModuleFile_IsWarningNotError()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        var manifestPath = dir.Combine(".aura", "project.json");
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["documentation"]!["backlog"] = "docs/03-BACKLOG.md";
        manifest["modules"] = new System.Text.Json.Nodes.JsonArray("BACKLOG");
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, d => d.Code == DiagnosticCode.MissingOptionalModule);
    }

    [Fact]
    public void Validate_MissingAgentsMd_IsInfoNotError()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.True(result.IsValid);
        Assert.Contains(result.Infos, d => d.Code == DiagnosticCode.AgentsFileAbsent);
    }

    [Fact]
    public void Validate_AbsentMachineProfile_IsInfoAndDoesNotInvalidateProject()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.True(result.IsValid);
        Assert.Contains(result.Infos, d => d.Code == DiagnosticCode.MachineProfileAbsent);
    }

    [Fact]
    public void Validate_InvalidMachineProfile_IsWarningAndDoesNotInvalidateProject()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        var machineProfilePath = dir.Combine("machine-profile", "machine.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(machineProfilePath)!);
        File.WriteAllText(machineProfilePath, """{ "schemaVersion": "1.0", "machineId": "not-a-uuid" }""");

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: machineProfilePath);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, d => d.Code == DiagnosticCode.MachineProfileInvalid);
    }

    [Fact]
    public void Validate_ValidMachineProfile_ProducesNoDiagnostic()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        var machineProfilePath = dir.Combine("machine-profile", "machine.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(machineProfilePath)!);
        var validJson = "{ \"schemaVersion\": \"1.0\", \"machineId\": \"" + Guid.NewGuid() +
            "\", \"machineName\": \"test-machine\" }";
        File.WriteAllText(machineProfilePath, validJson);

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: machineProfilePath);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == DiagnosticCode.MachineProfileInvalid);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == DiagnosticCode.MachineProfileAbsent);
    }

    [Fact]
    public void Validate_WorksWithoutGit()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        Assert.False(Directory.Exists(dir.Combine(".git")));

        var result = ProjectMemoryValidator.Validate(dir.Path, machineProfilePath: NonExistentMachineProfilePath(dir));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CopyingAnInitializedProject_PreservesProjectId()
    {
        using var source = new TempDirectory();
        using var destinationParent = new TempDirectory();

        var initResult = ProjectMemoryInitializer.Init(source.Path, "Portability Source");

        var destination = System.IO.Path.Combine(destinationParent.Path, "copy");
        CopyDirectory(source.Path, destination);

        var validation = ProjectMemoryValidator.Validate(destination, machineProfilePath: NonExistentMachineProfilePath(destinationParent));

        Assert.True(validation.IsValid);
        Assert.Equal(initResult.ProjectId, validation.ProjectId);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file)));
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(subDir, System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(subDir)));
        }
    }
}
