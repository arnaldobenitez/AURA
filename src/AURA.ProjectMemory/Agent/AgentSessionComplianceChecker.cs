using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Integrity;
using AURA.ProjectMemory.Paths;
using AURA.ProjectMemory.Resolution;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Agent;

/// <summary>
/// Verifies objective, checkable facts before an agent closes a session
/// (AURA_AGENT_CONTRACT v1.0 §8). Does not perform semantic inference; it only rejects
/// conditions that are demonstrably unsafe (no active session, wrong project, invalid Core,
/// unresolved conflict markers).
/// </summary>
public static class AgentSessionComplianceChecker
{
    public static AgentComplianceResult Check(
        string projectPath,
        string sessionId,
        string? runtimeRoot = null)
    {
        var issues = new List<Diagnostic>();

        var validation = ProjectMemoryValidator.Validate(projectPath);
        if (!validation.IsValid)
        {
            issues.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.ProjectMemoryInvalid,
                "Project Memory Core is invalid; cannot close a session safely.",
                validation.ProjectRoot));
        }

        var resolution = ProjectRootResolver.Resolve(projectPath);
        if (resolution.Manifest is null || resolution.ProjectRoot is null)
        {
            issues.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.ProjectRootNotFound,
                "Could not resolve PROJECT_ROOT from the supplied path."));
            return new AgentComplianceResult(false, null, issues);
        }

        var (lookupStatus, state, foundProjectId) = SessionStateStore.Find(sessionId, runtimeRoot);
        if (lookupStatus == SessionLookupStatus.NotFound)
        {
            issues.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.SessionNotFound,
                $"No open session with id '{sessionId}' was found.",
                sessionId));
            return new AgentComplianceResult(false, null, issues);
        }

        if (lookupStatus == SessionLookupStatus.Closed)
        {
            issues.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.SessionAlreadyClosed,
                $"Session '{sessionId}' was already closed.",
                sessionId));
            return new AgentComplianceResult(false, sessionId, issues);
        }

        if (state is not null && state.ProjectId != resolution.Manifest.ProjectId)
        {
            issues.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCode.SessionProjectMismatch,
                $"Session '{sessionId}' belongs to project '{state.ProjectId}', not the current project '{resolution.Manifest.ProjectId}'.",
                sessionId));
        }

        if (RelativePathResolver.TryResolveSafePath(
                resolution.ProjectRoot,
                resolution.Manifest.Documentation.CurrentState,
                out var currentStatePath) &&
            File.Exists(currentStatePath))
        {
            var content = File.ReadAllText(currentStatePath);
            if (ConflictMarkerDetector.ContainsConflictMarkers(content))
            {
                issues.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCode.CurrentStateConflictMarkers,
                    $"{resolution.Manifest.Documentation.CurrentState} contains unresolved Git merge conflict markers and cannot be trusted as current state.",
                    resolution.Manifest.Documentation.CurrentState));
            }
        }

        return new AgentComplianceResult(issues.Count == 0, sessionId, issues);
    }
}

public sealed record AgentComplianceResult(
    bool IsCompliant,
    string? SessionId,
    IReadOnlyList<Diagnostic> Issues);
