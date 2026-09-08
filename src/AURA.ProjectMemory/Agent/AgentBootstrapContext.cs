using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Agent;

/// <summary>
/// Universal, provider-neutral bootstrap context for an AI agent working on an AURA Project Memory
/// project. Returned by <see cref="AgentBootstrapService"/>. Contains enough structured
/// information for the agent to decide whether to open a session, what to read, and how to close.
/// </summary>
public sealed record AgentBootstrapContext(
    AgentProjectInfo Project,
    AgentProjectMemoryInfo ProjectMemory,
    AgentMachineInfo? Machine,
    AgentGitInfo Git,
    AgentSessionInfo Sessions,
    AgentContractPolicy AgentContract,
    AgentLifecycleCommands Commands,
    AgentDocumentationPolicy Documentation,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record AgentProjectInfo(
    bool Valid,
    string? ProjectRoot,
    string? ProjectId,
    string? ProjectName,
    IReadOnlyList<Diagnostic> ValidationDiagnostics);

public sealed record AgentProjectMemoryInfo(
    string Version,
    IReadOnlyList<string> CoreDocuments,
    IReadOnlyList<AgentModuleInfo> Modules);

public sealed record AgentModuleInfo(
    string Name,
    string? Path,
    bool Present);

public sealed record AgentMachineInfo(
    bool Present,
    string? MachineId,
    string? MachineName,
    string? ProfilePath);

public sealed record AgentGitInfo(
    bool Available,
    bool Repository,
    string? Branch,
    string? Commit,
    string? WorkingTree,
    bool HasMergeConflicts);

public sealed record AgentSessionInfo(
    IReadOnlyList<SessionState> OpenSessions,
    bool HasOpenSession);

public sealed record AgentContractPolicy(
    bool SessionRequiredForRelevantWork,
    bool HistoryAppendOnly,
    bool UpdateCurrentStateWhenProjectStateChanges,
    IReadOnlyList<string> RelevantWorkExamples,
    IReadOnlyList<string> IrrelevantWorkExamples,
    bool HandoffAllowed);

public sealed record AgentLifecycleCommands(
    string Bootstrap,
    string Start,
    string Status,
    string Close);

public sealed record AgentDocumentationPolicy(
    IReadOnlyList<AgentDocumentMapping> Mappings);

public sealed record AgentDocumentMapping(
    string Change,
    string Document);
