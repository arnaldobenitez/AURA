using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Session;

namespace AURA.ProjectMemory.Manager;

public sealed record ManagerDocument(string Name, string Path, bool Present);

public sealed record ManagerMachine(
    bool Present,
    bool Valid,
    string? MachineId,
    string? MachineName,
    string? ProfilePath);

public sealed record ManagerGit(
    bool Available,
    bool Repository,
    string? Branch,
    string? Commit,
    string WorkingTree,
    bool MergeConflicts);

public sealed record ManagerHistoryEntry(
    string FileName,
    string? SessionId,
    string? Timestamp,
    string? Slug,
    string Path,
    string? Title,
    string? Summary);

public sealed record ProjectManagerInspection(
    string? ProjectRoot,
    string? ProjectId,
    string? ProjectName,
    string? ProjectMemoryVersion,
    bool Valid,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? CurrentStatePath,
    IReadOnlyList<ManagerDocument> CoreDocuments,
    IReadOnlyList<ManagerDocument> OptionalModules,
    ManagerMachine Machine,
    ManagerGit Git,
    IReadOnlyList<SessionState> OpenSessions,
    IReadOnlyList<ManagerHistoryEntry> LastHistoryEntries);

public sealed record ProjectManagerHandoff(
    string? ProjectRoot,
    string? ProjectId,
    string? ProjectName,
    bool Valid,
    string? CurrentStatePath,
    string? CurrentStateSummary,
    IReadOnlyList<SessionState> OpenSessions,
    IReadOnlyList<ManagerHistoryEntry> RecentHistory,
    ManagerGit Git,
    IReadOnlyList<string> NextSteps,
    IReadOnlyList<Diagnostic> Diagnostics,
    string RecommendedBootstrapCommand);

public sealed record RememberedProject(
    string ProjectId,
    string? Alias,
    string ProjectName,
    string ProjectRoot,
    string RememberedAt,
    string LastSeenAt);

public sealed record KnownProject(
    RememberedProject Entry,
    bool PathExists,
    bool Valid,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record ManagerOperationResult<T>(
    bool Success,
    T? Value,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record ProjectManagerIndex(IReadOnlyList<RememberedProject> Projects);
