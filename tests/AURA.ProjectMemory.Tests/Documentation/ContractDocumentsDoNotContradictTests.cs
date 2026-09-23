using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Documentation;

/// <summary>
/// Static document checks: ensure the agent contract, AURA.md template, AGENTS.md template and
/// CLAUDE.md all reference the same key concepts and do not contradict each other. These are not
/// semantic tests, but they catch drift between the documents.
/// </summary>
public class ContractDocumentsDoNotContradictTests
{
    [Fact]
    public void AURA_TEMPLATE_MentionsAgentContract()
    {
        var auraMd = TestFile("docs/aura/project-memory/templates/AURA.md");

        Assert.Contains("AURA_AGENT_CONTRACT", auraMd);
        Assert.Contains("aura agent bootstrap", auraMd);
    }

    [Fact]
    public void AGENTS_TEMPLATE_MentionsAgentContract()
    {
        var agentsMd = TestFile("docs/aura/project-memory/templates/AGENTS.md");

        Assert.Contains("AURA_AGENT_CONTRACT", agentsMd);
        Assert.Contains("aura agent bootstrap", agentsMd);
    }

    [Fact]
    public void CLAUDE_MD_ReferencesBootstrapAndSessionCommands()
    {
        var claudeMd = TestFile("CLAUDE.md");

        Assert.Contains("aura agent bootstrap", claudeMd);
        Assert.Contains("aura project session start", claudeMd);
        Assert.Contains("aura project session close", claudeMd);
    }

    [Fact]
    public void LegacyRulesPointerNamesCurrentSourcesAndDoesNotAuthorizeLegacyContent()
    {
        var rules = TestFile("rules/AURA.md");

        Assert.Contains("LEGACY_COMPATIBILITY_POINTER", rules);
        Assert.Contains("AURA_PROTOCOL_v2.2.md", rules);
        Assert.Contains("AURA_PROJECT_MEMORY_v1.0.md", rules);
        Assert.Contains("AURA_AGENT_CONTRACT_v1.0.md", rules);
        Assert.Contains("no prevalece", rules);
        Assert.Contains("Ante cualquier contradicción", rules);
    }

    [Fact]
    public void AGENT_CONTRACT_ReferencesProjectMemoryVersion()
    {
        var contract = TestFile("docs/aura/AURA_AGENT_CONTRACT_v1.0.md");

        Assert.Contains("AURA_PROJECT_MEMORY v1.0", contract);
        Assert.Contains("aura agent bootstrap", contract);
        Assert.Contains("session start", contract);
        Assert.Contains("session close", contract);
    }

    [Fact]
    public void CLAUDE_MD_DoesNotMentionFictitiousAdrPath()
    {
        var claudeMd = TestFile("CLAUDE.md");
        Assert.DoesNotContain("docs/ADR.md", claudeMd);
    }

    [Fact]
    public void AGENT_CONTRACT_DoesNotMentionFictitiousAdrPath()
    {
        var contract = TestFile("docs/aura/AURA_AGENT_CONTRACT_v1.0.md");
        Assert.DoesNotContain("docs/ADR.md", contract);
    }

    [Fact]
    public void SESSION_TEMPLATE_UpdatedDocumentsIsOptional()
    {
        var session = TestFile("docs/aura/project-memory/templates/SESSION.md");

        Assert.Contains("updatedDocuments", session);
        Assert.Contains("omitir", session);
    }

    private static string TestFile(string relativePath)
    {
        var root = CliProcessTestsRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string CliProcessTestsRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AURA.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate AURA.sln.");
    }
}
