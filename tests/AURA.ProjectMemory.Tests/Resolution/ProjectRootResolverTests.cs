using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Resolution;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Resolution;

public class ProjectRootResolverTests
{
    [Fact]
    public void Resolve_FromProjectRootItself_FindsIt()
    {
        using var dir = new TempDirectory();
        var init = ProjectMemoryInitializer.Init(dir.Path);

        var resolution = ProjectRootResolver.Resolve(dir.Path);

        Assert.True(resolution.Found);
        Assert.Equal(dir.Path, resolution.ProjectRoot);
        Assert.Equal(init.ProjectId, resolution.Manifest!.ProjectId);
    }

    [Fact]
    public void Resolve_FromChildDirectory_FindsAncestorRoot()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path);
        var childPath = dir.Combine("src", "deep", "nested");
        Directory.CreateDirectory(childPath);

        var resolution = ProjectRootResolver.Resolve(childPath);

        Assert.True(resolution.Found);
        Assert.Equal(dir.Path, resolution.ProjectRoot);
    }

    [Fact]
    public void Resolve_NestedProject_PrefersTheClosestRoot()
    {
        using var dir = new TempDirectory();
        var outerResult = ProjectMemoryInitializer.Init(dir.Path, "Outer");
        var innerPath = dir.Combine("apps", "app1");
        Directory.CreateDirectory(innerPath);
        var innerResult = ProjectMemoryInitializer.Init(innerPath, "Inner");
        var deepChild = System.IO.Path.Combine(innerPath, "src");
        Directory.CreateDirectory(deepChild);

        var resolution = ProjectRootResolver.Resolve(deepChild);

        Assert.True(resolution.Found);
        Assert.Equal(innerPath, resolution.ProjectRoot);
        Assert.Equal(innerResult.ProjectId, resolution.Manifest!.ProjectId);
        Assert.NotEqual(outerResult.ProjectId, resolution.Manifest!.ProjectId);
    }

    [Fact]
    public void Resolve_InvalidNearestManifest_StopsAndReportsErrorInsteadOfSearchingHigher()
    {
        using var dir = new TempDirectory();
        ProjectMemoryInitializer.Init(dir.Path, "Outer");
        var innerPath = dir.Combine("apps", "app1");
        Directory.CreateDirectory(System.IO.Path.Combine(innerPath, ".aura"));
        File.WriteAllText(System.IO.Path.Combine(innerPath, ".aura", "project.json"), "{ not valid json");

        var resolution = ProjectRootResolver.Resolve(innerPath);

        Assert.False(resolution.Found);
        Assert.Contains(resolution.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(resolution.Diagnostics, d => d.Code == DiagnosticCode.ProjectRootNotFound);
    }

    [Fact]
    public void Resolve_NoProjectAnywhere_ReturnsNotFound()
    {
        using var dir = new TempDirectory();

        var resolution = ProjectRootResolver.Resolve(dir.Path, workspaceBoundary: dir.Path);

        Assert.False(resolution.Found);
        Assert.Contains(resolution.Diagnostics, d => d.Code == DiagnosticCode.ProjectRootNotFound);
    }

    [Fact]
    public void Resolve_DoesNotSearchAboveTheWorkspaceBoundary()
    {
        using var dir = new TempDirectory();
        var boundary = dir.Combine("workspace");
        Directory.CreateDirectory(boundary);
        // A project root above the boundary must be invisible to a search bounded at "workspace".
        ProjectMemoryInitializer.Init(dir.Path, "AboveBoundary");
        var insideBoundary = System.IO.Path.Combine(boundary, "child");
        Directory.CreateDirectory(insideBoundary);

        var resolution = ProjectRootResolver.Resolve(insideBoundary, workspaceBoundary: boundary);

        Assert.False(resolution.Found);
    }
}
