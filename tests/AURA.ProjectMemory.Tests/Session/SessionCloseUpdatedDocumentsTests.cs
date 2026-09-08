using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Session;

public class SessionCloseUpdatedDocumentsTests
{
    [Fact]
    public void Close_WithUpdatedDocuments_RendersThemInHistory()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Updated Docs");

        var start = SessionManager.Start(project.Path,
            new SessionStartRequest("test", "role"), runtime.Path);
        var close = SessionManager.Close(project.Path, start.SessionId!,
            new SessionCloseRequest("done", "none", "next",
                UpdatedDocuments: ["docs/01-ESTADO-ACTUAL.md", "docs/04-ARQUITECTURA.md"]),
            runtime.Path);

        Assert.True(close.Success);
        var history = File.ReadAllText(project.Combine(close.HistoryFile!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("`updatedDocuments`", history);
        Assert.Contains("docs/01-ESTADO-ACTUAL.md", history);
        Assert.Contains("docs/04-ARQUITECTURA.md", history);
    }

    [Fact]
    public void Close_WithoutUpdatedDocuments_OmitsField()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "No Updated Docs");

        var start = SessionManager.Start(project.Path,
            new SessionStartRequest("test", "role"), runtime.Path);
        var close = SessionManager.Close(project.Path, start.SessionId!,
            new SessionCloseRequest("done", "none", "next"), runtime.Path);

        Assert.True(close.Success);
        var history = File.ReadAllText(project.Combine(close.HistoryFile!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("updatedDocuments", history);
    }
}
