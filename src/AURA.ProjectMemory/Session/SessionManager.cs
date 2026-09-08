using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Paths;
using AURA.ProjectMemory.Resolution;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Session;

/// <summary>
/// Orchestrates the session lifecycle (AURA_PROJECT_MEMORY v1.0 §9/§10/§11) on top of the
/// existing, unmodified <see cref="ProjectRootResolver"/> and <see cref="ProjectMemoryValidator"/>
/// from Etapa 4: it never re-implements PROJECT_ROOT detection or Core validation, it only adds
/// what those didn't have - opening, tracking, and durably closing a session.
/// </summary>
public static class SessionManager
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:sszzz";

    public static SessionStartResult Start(
        string startPath, SessionStartRequest request, string? runtimeRoot = null)
    {
        var validation = ProjectMemoryValidator.Validate(startPath);
        if (!validation.IsValid || validation.ProjectRoot is null || validation.ProjectId is null)
        {
            return new SessionStartResult(false, null, null, null, validation.Diagnostics);
        }

        var diagnostics = new List<Diagnostic>();
        var machine = MachineProfileValidator.Validate(request.MachineProfilePath);
        string? machineId = null;
        string? machineName = null;

        if (machine.IsValid)
        {
            machineId = machine.MachineId;
            machineName = machine.MachineName;
        }
        else if (machine.Diagnostics.Any(d => d.Code == DiagnosticCode.MachineProfileAbsent))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                DiagnosticCode.MachineProfileMissingForSession,
                "No local machine profile is configured; this session will not carry a persistent " +
                "machineId. Run 'aura machine init' to enable it for future sessions.",
                machine.ProfilePath));
        }
        else
        {
            diagnostics.AddRange(machine.Diagnostics);
        }

        var sessionId = Guid.NewGuid().ToString();
        var startedAt = DateTimeOffset.Now.ToString(TimestampFormat);

        // Git is strictly optional (AURA_PROJECT_MEMORY v1.0 Etapa 6 §7/§15/§16): its own
        // "unavailable"/"not a repository" diagnostics are Info-level and never surfaced here to
        // avoid noise for the common case of a project with no Git at all.
        var git = GitContextProvider.Capture(validation.ProjectRoot);
        diagnostics.AddRange(git.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));

        var state = new SessionState(
            sessionId,
            validation.ProjectId,
            startedAt,
            request.AuraRole,
            request.Objective,
            validation.ProjectRoot,
            request.LogicalUser,
            request.Agent,
            request.Model,
            SessionFileNameGenerator.Sanitize(request.Slug),
            machineId,
            machineName,
            git.Branch,
            git.Commit,
            WorkingTreeToString(git.WorkingTree));

        SessionStateStore.Save(state, runtimeRoot);

        return new SessionStartResult(true, sessionId, validation.ProjectId, startedAt, diagnostics);
    }

    public static SessionCloseResult Close(
        string startPath, string sessionId, SessionCloseRequest request, string? runtimeRoot = null)
    {
        var validation = ProjectMemoryValidator.Validate(startPath);
        if (!validation.IsValid || validation.ProjectRoot is null || validation.ProjectId is null)
        {
            return new SessionCloseResult(false, sessionId, null, null, null, validation.Diagnostics);
        }

        var resolution = ProjectRootResolver.Resolve(startPath);
        if (!resolution.Found || resolution.Manifest is null)
        {
            return new SessionCloseResult(false, sessionId, null, null, null, resolution.Diagnostics);
        }

        var (status, state, foundProjectId) = SessionStateStore.Find(sessionId, runtimeRoot);

        switch (status)
        {
            case SessionLookupStatus.NotFound:
                return Failure(sessionId, DiagnosticCode.SessionNotFound,
                    $"No open session with id '{sessionId}' was found.");
            case SessionLookupStatus.Closed:
                return Failure(sessionId, DiagnosticCode.SessionAlreadyClosed,
                    $"Session '{sessionId}' was already closed. It cannot be closed again.");
        }

        if (foundProjectId != validation.ProjectId || state is null)
        {
            return Failure(sessionId, DiagnosticCode.SessionProjectMismatch,
                $"Session '{sessionId}' belongs to project '{foundProjectId}', not the current project " +
                $"('{validation.ProjectId}'). Refusing to write history into the wrong project.");
        }

        if (string.IsNullOrWhiteSpace(request.Result) ||
            string.IsNullOrWhiteSpace(request.Pending) ||
            string.IsNullOrWhiteSpace(request.NextRecommendedStep))
        {
            return Failure(sessionId, DiagnosticCode.InvalidSessionPayload,
                "'result', 'pending' and 'nextRecommendedStep' are mandatory and cannot be empty.");
        }

        var manifest = resolution.Manifest;
        var projectRoot = resolution.ProjectRoot!;

        if (!RelativePathResolver.TryResolveSafePath(projectRoot, manifest.Documentation.History, out var historyDir))
        {
            return Failure(sessionId, DiagnosticCode.HistoryWriteFailed,
                $"documentation.history ('{manifest.Documentation.History}') escapes PROJECT_ROOT.");
        }

        var diagnostics = new List<Diagnostic>();

        // Final Git snapshot, resolved against §14's rule: an automatically captured value always
        // wins over a manually supplied one; a genuine conflict between the two is surfaced as a
        // warning rather than silently discarded or silently overwritten.
        var git = GitContextProvider.Capture(projectRoot);
        diagnostics.AddRange(git.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));

        var effectiveRequest = request with
        {
            GitBranch = ResolveGitField("gitBranch", git.Branch ?? state.GitBranch, request.GitBranch, diagnostics),
            InitialCommit = ResolveGitField("initialCommit", state.InitialCommit, request.InitialCommit, diagnostics),
            FinalCommit = ResolveGitField("finalCommit", git.Commit, request.FinalCommit, diagnostics),
            WorkingTreeInitial = ResolveGitField(
                "workingTreeInitial", state.WorkingTreeInitial, request.WorkingTreeInitial, diagnostics),
            WorkingTreeFinal = ResolveGitField(
                "workingTreeFinal", WorkingTreeToString(git.WorkingTree), request.WorkingTreeFinal, diagnostics),
        };

        var finishedAt = DateTimeOffset.Now.ToString(TimestampFormat);
        var fileName = SessionFileNameGenerator.Generate(DateTimeOffset.Now, sessionId, state.Slug);
        var fullPath = Path.Combine(historyDir, fileName);
        var content = SessionRecordRenderer.Render(state, effectiveRequest, finishedAt);

        try
        {
            Directory.CreateDirectory(historyDir);
            using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }
        catch (IOException) when (File.Exists(fullPath))
        {
            return Failure(sessionId, DiagnosticCode.SessionHistoryCollision,
                $"A history file already exists at '{fileName}'. The session was not closed; retry.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure(sessionId, DiagnosticCode.HistoryWriteFailed,
                $"Could not write the history file: {ex.Message}");
        }

        var portableHistoryFile = RelativePathResolver.ToPortableRelativePath(projectRoot, fullPath);
        SessionStateStore.MarkClosed(validation.ProjectId, sessionId, finishedAt, portableHistoryFile, runtimeRoot);

        return new SessionCloseResult(true, sessionId, validation.ProjectId, portableHistoryFile, finishedAt, diagnostics);
    }

    private static string? WorkingTreeToString(WorkingTreeStatus status) => status switch
    {
        WorkingTreeStatus.Clean => "clean",
        WorkingTreeStatus.Dirty => "dirty",
        _ => null
    };

    /// <summary>
    /// Applies the manual-vs-automatic policy from AURA_PROJECT_MEMORY v1.0 Etapa 6 §14: an
    /// automatically captured value always wins; a manual value is only used as a fallback when
    /// nothing could be captured. A genuine mismatch between the two produces a warning instead
    /// of silently discarding either one.
    /// </summary>
    private static string? ResolveGitField(
        string fieldName, string? captured, string? manual, List<Diagnostic> diagnostics)
    {
        if (captured is null)
        {
            return manual;
        }

        if (manual is not null && !string.Equals(captured, manual, StringComparison.Ordinal))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCode.GitMetadataMismatch,
                $"{fieldName}: the automatically captured value ('{captured}') differs from the manually " +
                $"supplied value ('{manual}'). The captured value was used."));
        }

        return captured;
    }

    public static SessionStatusResult Status(string startPath, string? runtimeRoot = null)
    {
        var resolution = ProjectRootResolver.Resolve(startPath);
        if (!resolution.Found || resolution.ProjectRoot is null || resolution.Manifest is null)
        {
            return new SessionStatusResult(false, null, null, [], resolution.Diagnostics);
        }

        var sessions = SessionStateStore.ListActive(resolution.Manifest.ProjectId, runtimeRoot);
        return new SessionStatusResult(true, resolution.ProjectRoot, resolution.Manifest.ProjectId, sessions, []);
    }

    private static SessionCloseResult Failure(string sessionId, string code, string message) =>
        new(false, sessionId, null, null, null, [new Diagnostic(DiagnosticSeverity.Error, code, message)]);
}
