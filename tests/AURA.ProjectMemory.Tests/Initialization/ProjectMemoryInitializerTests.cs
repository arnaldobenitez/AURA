using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Initialization;

public class ProjectMemoryInitializerTests
{
    [Fact]
    public void Init_OnEmptyDirectory_CreatesExactlyTheCore()
    {
        using var dir = new TempDirectory();

        var result = ProjectMemoryInitializer.Init(dir.Path, "Example Project");

        Assert.True(result.Success);
        Assert.Equal(dir.Path, result.ProjectRoot);
        Assert.NotNull(result.ProjectId);
        Assert.True(Guid.TryParseExact(result.ProjectId, "D", out _));

        Assert.True(File.Exists(dir.Combine("AURA.md")));
        Assert.True(File.Exists(dir.Combine(".aura", "project.json")));
        Assert.True(File.Exists(dir.Combine("docs", "00-PROYECTO.md")));
        Assert.True(File.Exists(dir.Combine("docs", "01-ESTADO-ACTUAL.md")));
        Assert.True(Directory.Exists(dir.Combine("docs", "historial")));

        Assert.False(File.Exists(dir.Combine("AGENTS.md")));
        Assert.False(File.Exists(dir.Combine("docs", "02-DECISIONES.md")));
        Assert.False(File.Exists(dir.Combine("docs", "03-BACKLOG.md")));
        Assert.False(File.Exists(dir.Combine("docs", "04-ARQUITECTURA.md")));
    }

    [Fact]
    public void Init_GeneratesUuidV4ProjectId()
    {
        using var dir = new TempDirectory();

        var result = ProjectMemoryInitializer.Init(dir.Path);

        var guid = Guid.Parse(result.ProjectId!);
        Assert.Equal(4, (guid.ToByteArray()[7] >> 4) & 0x0F);
    }

    [Fact]
    public void Init_WithoutExplicitName_UsesDirectoryNameAsProjectName()
    {
        using var dir = new TempDirectory();
        var expectedName = new DirectoryInfo(dir.Path).Name;

        ProjectMemoryInitializer.Init(dir.Path);

        var manifestJson = File.ReadAllText(dir.Combine(".aura", "project.json"));
        Assert.Contains($"\"projectName\": \"{expectedName}\"", manifestJson);
    }

    [Fact]
    public void Init_Twice_DoesNotRegenerateIdentityOrOverwriteFiles()
    {
        using var dir = new TempDirectory();

        var first = ProjectMemoryInitializer.Init(dir.Path, "Original Name");
        var manifestBefore = File.ReadAllText(dir.Combine(".aura", "project.json"));

        var second = ProjectMemoryInitializer.Init(dir.Path, "Different Name");
        var manifestAfter = File.ReadAllText(dir.Combine(".aura", "project.json"));

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains(second.Diagnostics, d => d.Code == DiagnosticCode.AlreadyInitialized);
        Assert.Equal(manifestBefore, manifestAfter);
    }

    [Fact]
    public void Init_PreservesPreExistingFilesOfABusinessProject()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(dir.Combine("src"));
        File.WriteAllText(dir.Combine("src", "app.cs"), "// business code");
        File.WriteAllText(dir.Combine("README.md"), "# existing readme");

        var result = ProjectMemoryInitializer.Init(dir.Path);

        Assert.True(result.Success);
        Assert.Equal("// business code", File.ReadAllText(dir.Combine("src", "app.cs")));
        Assert.Equal("# existing readme", File.ReadAllText(dir.Combine("README.md")));
    }

    [Fact]
    public void Init_ManifestContentIsValidAgainstProjectSchema()
    {
        using var dir = new TempDirectory();

        ProjectMemoryInitializer.Init(dir.Path, "Schema Check");

        var json = File.ReadAllText(dir.Combine(".aura", "project.json"));
        var node = AURA.ProjectMemory.Manifest.ProjectManifestSerializer.ParseToNode(json)!;
        var element = System.Text.Json.JsonSerializer.SerializeToElement(node);
        var evaluation = AURA.ProjectMemory.Schemas.ManifestSchemaProvider.ProjectSchema.Evaluate(element);

        Assert.True(evaluation.IsValid);
    }

    [Fact]
    public void Init_ManifestUsesForwardSlashRelativePaths()
    {
        using var dir = new TempDirectory();

        ProjectMemoryInitializer.Init(dir.Path);

        var json = File.ReadAllText(dir.Combine(".aura", "project.json"));
        Assert.Contains("docs/00-PROYECTO.md", json);
        Assert.Contains("docs/01-ESTADO-ACTUAL.md", json);
        Assert.Contains("docs/historial", json);
        Assert.DoesNotContain(@"\\", json);
    }

    [Fact]
    public void Init_WorksWithoutGit()
    {
        using var dir = new TempDirectory();
        Assert.False(Directory.Exists(dir.Combine(".git")));

        var result = ProjectMemoryInitializer.Init(dir.Path);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(dir.Combine(".git")));
    }

    [Fact]
    public void Init_AuraMdWithoutManifest_ReportsPartialInitializationAndDoesNotOverwrite()
    {
        using var dir = new TempDirectory();
        var auraPath = dir.Combine("AURA.md");
        File.WriteAllText(auraPath, "existing AURA instructions");

        var result = ProjectMemoryInitializer.Init(dir.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.PartialInitialization);
        Assert.Equal("existing AURA instructions", File.ReadAllText(auraPath));
        Assert.False(File.Exists(dir.Combine(".aura", "project.json")));
    }

    [Fact]
    public void Init_ProjectDocumentWithoutManifest_ReportsPartialInitializationAndDoesNotOverwrite()
    {
        using var dir = new TempDirectory();
        var projectPath = dir.Combine("docs", "00-PROYECTO.md");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "existing project document");

        var result = ProjectMemoryInitializer.Init(dir.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.PartialInitialization);
        Assert.Equal("existing project document", File.ReadAllText(projectPath));
        Assert.False(File.Exists(dir.Combine(".aura", "project.json")));
    }

    [Fact]
    public void Init_EmptyDirectoryStillCreatesTheCoreAfterPartialDetectionWasAdded()
    {
        using var dir = new TempDirectory();

        var result = ProjectMemoryInitializer.Init(dir.Path);

        Assert.True(result.Success);
        Assert.NotNull(result.ProjectId);
        Assert.True(File.Exists(dir.Combine(".aura", "project.json")));
    }
}
