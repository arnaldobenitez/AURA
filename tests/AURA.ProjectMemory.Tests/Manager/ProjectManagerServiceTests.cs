using System.Text.Json;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Manager;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Manager;

public class ProjectManagerServiceTests
{
    [Fact]
    public void ListKnown_WhenIndexIsMissing_ReturnsEmpty()
    {
        using var index = new TempDirectory();

        var result = ProjectManagerService.ListKnown(index.Combine("projects", "index.json"));

        Assert.Empty(result);
    }

    [Fact]
    public void Remember_NewProject_IsIdempotent()
    {
        using var project = CreateProject("Manager Test");
        using var index = new TempDirectory();
        var indexPath = index.Combine("projects", "index.json");

        var first = ProjectManagerService.Remember(project.Path, "aura", indexPath);
        var second = ProjectManagerService.Remember(project.Path, "aura", indexPath);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Single(ProjectManagerIndexStore.Load(indexPath).Projects);
        Assert.Equal(first.Value!.ProjectId, second.Value!.ProjectId);
    }

    [Fact]
    public void Remember_UpdatesPathAndLastSeen()
    {
        using var project = CreateProject("Manager Test");
        using var index = new TempDirectory();
        var indexPath = index.Combine("index.json");
        var manifest = JsonDocument.Parse(File.ReadAllText(project.Combine(".aura", "project.json")));
        var id = manifest.RootElement.GetProperty("projectId").GetString()!;
        var oldTime = "2020-01-01T00:00:00Z";
        ProjectManagerIndexStore.Save(new ProjectManagerIndex([
            new RememberedProject(id, "old", "Old", "C:\\old-project", oldTime, oldTime)
        ]), indexPath);

        var result = ProjectManagerService.Remember(project.Path, "new", indexPath);
        var entry = Assert.IsType<RememberedProject>(result.Value);

        Assert.Equal(project.Path, entry.ProjectRoot);
        Assert.Equal("new", entry.Alias);
        Assert.Equal(oldTime, entry.RememberedAt);
        Assert.NotEqual(oldTime, entry.LastSeenAt);
    }

    [Fact]
    public void Remember_DuplicateAlias_ReturnsError()
    {
        using var firstProject = CreateProject("First");
        using var secondProject = CreateProject("Second");
        using var index = new TempDirectory();
        var indexPath = index.Combine("index.json");

        Assert.True(ProjectManagerService.Remember(firstProject.Path, "same", indexPath).Success);
        var result = ProjectManagerService.Remember(secondProject.Path, "same", indexPath);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "PM_ALIAS_CONFLICT");
    }

    [Fact]
    public void Forget_WorksByAliasAndProjectId()
    {
        using var first = CreateProject("First");
        using var second = CreateProject("Second");
        using var index = new TempDirectory();
        var path = index.Combine("index.json");
        var firstEntry = ProjectManagerService.Remember(first.Path, "first", path).Value!;
        var secondEntry = ProjectManagerService.Remember(second.Path, "second", path).Value!;

        Assert.True(ProjectManagerService.Forget("first", path).Success);
        Assert.True(ProjectManagerService.Forget(secondEntry.ProjectId, path).Success);
        Assert.Empty(ProjectManagerIndexStore.Load(path).Projects);
    }

    [Fact]
    public void ListKnown_WithMissingPathReportsIt()
    {
        using var index = new TempDirectory();
        var path = index.Combine("index.json");
        var entry = new RememberedProject("00000000-0000-4000-8000-000000000001", "gone", "Gone", "C:\\gone", "now", "now");
        ProjectManagerIndexStore.Save(new ProjectManagerIndex([entry]), path);

        var result = ProjectManagerService.ListKnown(path);

        var known = Assert.Single(result);
        Assert.False(known.PathExists);
        Assert.False(known.Valid);
    }

    [Fact]
    public void Inspect_ValidProject_AndInvalidPathExposeDiagnostics()
    {
        using var project = CreateProject("Inspection");
        using var outside = new TempDirectory();

        var valid = ProjectManagerService.Inspect(project.Path);
        var invalid = ProjectManagerService.Inspect(outside.Path);

        Assert.True(valid.Valid);
        Assert.Equal("1.0", valid.ProjectMemoryVersion);
        Assert.Contains(valid.CoreDocuments, d => d.Path == "docs/01-ESTADO-ACTUAL.md" && d.Present);
        Assert.Contains(valid.OptionalModules, d => d.Name == "03-BACKLOG" && !d.Present);
        Assert.False(invalid.Valid);
        Assert.NotEmpty(invalid.Diagnostics);
    }

    [Fact]
    public void History_IsDescendingAndRespectsTake()
    {
        using var project = CreateProject("History");
        var history = project.Combine("docs", "historial");
        File.WriteAllText(Path.Combine(history, "2024-01-01T00-00-00Z__00000000-0000-4000-8000-000000000001__old.md"),
            "# Old\n\n## Resultado\n\nold result");
        File.WriteAllText(Path.Combine(history, "2025-01-01T00-00-00Z__00000000-0000-4000-8000-000000000002__new.md"),
            "# New\n\n## Resultado\n\nnew result");

        var result = ProjectManagerService.History(project.Path, 1);

        var item = Assert.Single(result);
        Assert.Equal("new", item.Slug);
        Assert.Equal("new result", item.Summary);
    }

    [Fact]
    public void HandoffIncludesStateGitAndHistoryWithoutCreatingSession()
    {
        using var project = CreateProject("Handoff");
        var before = Directory.GetFiles(project.Combine("docs", "historial"));

        var handoff = ProjectManagerService.Handoff(project.Path);

        Assert.Equal("Handoff", handoff.ProjectName);
        Assert.NotNull(handoff.ProjectId);
        Assert.NotNull(handoff.Git);
        Assert.Equal("docs/01-ESTADO-ACTUAL.md", handoff.CurrentStatePath);
        Assert.Empty(handoff.OpenSessions);
        Assert.Equal(before, Directory.GetFiles(project.Combine("docs", "historial")));
    }

    [Fact]
    public void ManagerReadsButDoesNotModifyCoreFiles()
    {
        using var project = CreateProject("Read Only");
        var files = new[] { "AURA.md", ".aura/project.json", "docs/00-PROYECTO.md", "docs/01-ESTADO-ACTUAL.md" };
        var before = files.ToDictionary(file => file, file => File.ReadAllText(project.Combine(file)));

        ProjectManagerService.Inspect(project.Path);
        ProjectManagerService.Handoff(project.Path);

        foreach (var file in files)
        {
            Assert.Equal(before[file], File.ReadAllText(project.Combine(file)));
        }
    }

    private static TempDirectory CreateProject(string name)
    {
        var project = new TempDirectory();
        Assert.True(ProjectMemoryInitializer.Init(project.Path, name).Success);
        return project;
    }
}
