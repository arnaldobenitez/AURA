using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Templates;

namespace AURA.ProjectMemory.Tests.Session;

public class SessionRecordRendererTests
{
    private static SessionState MakeState(
        string? machineId = null, string? machineName = null, string? logicalUser = null,
        string? agent = null, string? model = null, string? slug = null) => new(
        "550e8400-e29b-41d4-a716-446655440000",
        "6ba7b810-9dad-41d1-80b4-00c04fd430c8",
        "2026-09-04T15:10:44-03:00",
        "coding-agent",
        "Implement the renderer",
        "/tmp/hint",
        logicalUser,
        agent,
        model,
        slug,
        machineId,
        machineName);

    [Fact]
    public void Render_IncludesMandatoryFields()
    {
        var state = MakeState();
        var request = new SessionCloseRequest("Done", "Nothing", "Ship it");

        var rendered = SessionRecordRenderer.Render(state, request, "2026-09-04T16:00:00-03:00");

        Assert.Contains("# Sesión 550e8400-e29b-41d4-a716-446655440000", rendered);
        Assert.Contains("`sessionId` | `550e8400-e29b-41d4-a716-446655440000`", rendered);
        Assert.Contains("`projectId` | `6ba7b810-9dad-41d1-80b4-00c04fd430c8`", rendered);
        Assert.Contains("`startedAt` | `2026-09-04T15:10:44-03:00`", rendered);
        Assert.Contains("`finishedAt` | `2026-09-04T16:00:00-03:00`", rendered);
        Assert.Contains("## Objetivo", rendered);
        Assert.Contains("Implement the renderer", rendered);
        Assert.Contains("## Resultado", rendered);
        Assert.Contains("Done", rendered);
        Assert.Contains("## Pendientes", rendered);
        Assert.Contains("Nothing", rendered);
        Assert.Contains("## Próximo paso recomendado", rendered);
        Assert.Contains("Ship it", rendered);
    }

    [Fact]
    public void Render_OmitsOptionalFieldsWhenAbsent()
    {
        var state = MakeState();
        var request = new SessionCloseRequest("Done", "Nothing", "Ship it");

        var rendered = SessionRecordRenderer.Render(state, request, "2026-09-04T16:00:00-03:00");

        Assert.DoesNotContain("`machineId`", rendered);
        Assert.DoesNotContain("`machineName`", rendered);
        Assert.DoesNotContain("`logicalUser`", rendered);
        Assert.DoesNotContain("`agent`", rendered);
        Assert.DoesNotContain("`model`", rendered);
        Assert.DoesNotContain("`osUser`", rendered);
        Assert.DoesNotContain("`gitBranch`", rendered);
    }

    [Fact]
    public void Render_IncludesMachineMetadataWhenPresent()
    {
        var state = MakeState(machineId: "machine-id", machineName: "ARNALDO-2", logicalUser: "Arnaldo",
            agent: "Claude", model: "test-model");
        var request = new SessionCloseRequest("Done", "Nothing", "Ship it");

        var rendered = SessionRecordRenderer.Render(state, request, "2026-09-04T16:00:00-03:00");

        Assert.Contains("`machineId` | `machine-id`", rendered);
        Assert.Contains("`machineName` | `ARNALDO-2`", rendered);
        Assert.Contains("`logicalUser` | `Arnaldo`", rendered);
        Assert.Contains("`agent` | `Claude`", rendered);
        Assert.Contains("`model` | `test-model`", rendered);
    }

    [Fact]
    public void Render_IncludesOptionalGitFieldsWhenProvided()
    {
        var state = MakeState();
        var request = new SessionCloseRequest("Done", "Nothing", "Ship it",
            GitBranch: "master", InitialCommit: "abc123", FinalCommit: "def456",
            WorkingTreeInitial: "clean", WorkingTreeFinal: "dirty");

        var rendered = SessionRecordRenderer.Render(state, request, "2026-09-04T16:00:00-03:00");

        Assert.Contains("`gitBranch` | `master`", rendered);
        Assert.Contains("`initialCommit` | `abc123`", rendered);
        Assert.Contains("`finalCommit` | `def456`", rendered);
        Assert.Contains("`workingTreeInitial` | `clean`", rendered);
        Assert.Contains("`workingTreeFinal` | `dirty`", rendered);
    }

    [Fact]
    public void Render_RendersModifiedFilesAsBulletList()
    {
        var state = MakeState();
        var request = new SessionCloseRequest("Done", "Nothing", "Ship it",
            ModifiedFiles: ["src/a.cs", "src/b.cs"]);

        var rendered = SessionRecordRenderer.Render(state, request, "2026-09-04T16:00:00-03:00");

        Assert.Contains("- src/a.cs", rendered);
        Assert.Contains("- src/b.cs", rendered);
    }

    [Fact]
    public void Render_NeverIncludesTranscriptMarkersItDidNotReceive()
    {
        var state = MakeState();
        var request = new SessionCloseRequest("Done", "Nothing", "Ship it");

        var rendered = SessionRecordRenderer.Render(state, request, "2026-09-04T16:00:00-03:00");

        Assert.DoesNotContain("prompt", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("transcript", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_SectionOrderMatchesNormativeTemplate()
    {
        var template = TemplateProvider.GetSessionTemplate();
        var state = MakeState();
        var request = new SessionCloseRequest("R", "P", "N",
            WorkPerformed: "W", ModifiedFiles: ["f"], Decisions: "D", Tests: "T",
            Problems: "PR", TechnicalDebt: "TD");

        var rendered = SessionRecordRenderer.Render(state, request, "2026-09-04T16:00:00-03:00");

        var templateHeadings = System.Text.RegularExpressions.Regex.Matches(template, @"^##\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(h => h != "Metadatos")
            .ToList();

        var renderedHeadings = System.Text.RegularExpressions.Regex.Matches(rendered, @"^##\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(h => h != "Metadatos")
            .ToList();

        Assert.Equal(templateHeadings, renderedHeadings);
    }
}
