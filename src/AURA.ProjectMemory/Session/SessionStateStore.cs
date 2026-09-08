using System.Text.Json;

namespace AURA.ProjectMemory.Session;

/// <summary>
/// Ephemeral, out-of-project storage for open sessions (AURA_PROJECT_MEMORY v1.0 §13):
/// <c>~/.aura/runtime/sessions/&lt;projectId&gt;/&lt;sessionId&gt;.json</c> by default. This is never part
/// of Project Memory, never versioned, and never authoritative for history - it exists only so a
/// session opened in one process can be closed later (possibly by another invocation of the CLI).
/// <see cref="SessionId"/> is the sole selector; callers never need to know a project's path to
/// close a session, only its id (see <see cref="Find"/>).
/// </summary>
public static class SessionStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultRuntimeRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aura", "runtime", "sessions");

    public static void Save(SessionState state, string? runtimeRoot = null)
    {
        var projectDir = ProjectDirectory(state.ProjectId, runtimeRoot);
        Directory.CreateDirectory(projectDir);
        var path = Path.Combine(projectDir, state.SessionId + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }

    /// <summary>Searches every known project's session directory for <paramref name="sessionId"/>,
    /// since a session is looked up by id alone, never by path.</summary>
    public static (SessionLookupStatus Status, SessionState? State, string? ProjectId) Find(
        string sessionId, string? runtimeRoot = null)
    {
        var root = runtimeRoot ?? DefaultRuntimeRoot();
        if (!Directory.Exists(root))
        {
            return (SessionLookupStatus.NotFound, null, null);
        }

        foreach (var projectDir in Directory.GetDirectories(root))
        {
            var projectId = Path.GetFileName(projectDir);
            var activePath = Path.Combine(projectDir, sessionId + ".json");
            if (File.Exists(activePath))
            {
                var state = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(activePath), JsonOptions);
                return (SessionLookupStatus.Active, state, projectId);
            }

            var closedPath = Path.Combine(projectDir, sessionId + ".closed");
            if (File.Exists(closedPath))
            {
                return (SessionLookupStatus.Closed, null, projectId);
            }
        }

        return (SessionLookupStatus.NotFound, null, null);
    }

    public static IReadOnlyList<SessionState> ListActive(string projectId, string? runtimeRoot = null)
    {
        var projectDir = ProjectDirectory(projectId, runtimeRoot);
        if (!Directory.Exists(projectDir))
        {
            return [];
        }

        var result = new List<SessionState>();
        foreach (var file in Directory.GetFiles(projectDir, "*.json"))
        {
            var state = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(file), JsonOptions);
            if (state is not null)
            {
                result.Add(state);
            }
        }

        return result;
    }

    /// <summary>Atomically retires an active session: removes the active state and leaves a small
    /// tombstone so a later lookup can report "already closed" instead of "never existed".</summary>
    public static void MarkClosed(string projectId, string sessionId, string finishedAt, string historyFile,
        string? runtimeRoot = null)
    {
        var projectDir = ProjectDirectory(projectId, runtimeRoot);
        var activePath = Path.Combine(projectDir, sessionId + ".json");
        var closedPath = Path.Combine(projectDir, sessionId + ".closed");

        File.WriteAllText(closedPath, JsonSerializer.Serialize(new { finishedAt, historyFile }, JsonOptions));

        if (File.Exists(activePath))
        {
            File.Delete(activePath);
        }
    }

    private static string ProjectDirectory(string projectId, string? runtimeRoot) =>
        Path.Combine(runtimeRoot ?? DefaultRuntimeRoot(), projectId);
}
