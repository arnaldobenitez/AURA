using System.Text.Json;
using System.Text.Json.Nodes;
using AURA.ProjectMemory.Agent;
using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Manager;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Cli;

/// <summary>
/// Renders <c>--json</c> output for machine consumers (e.g. a future AURA Project Manager).
/// This is a presentation-only concern; the reusable result types live in AURA.ProjectMemory.
/// </summary>
internal static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ForValidation(ProjectValidationResult result)
    {
        var root = new JsonObject
        {
            ["valid"] = result.IsValid,
            ["projectRoot"] = result.ProjectRoot,
            ["projectId"] = result.ProjectId,
            ["errors"] = ToArray(result.Errors),
            ["warnings"] = ToArray(result.Warnings),
            ["info"] = ToArray(result.Infos)
        };

        return root.ToJsonString(Options);
    }

    public static string ForInit(ProjectInitResult result)
    {
        var root = new JsonObject
        {
            ["success"] = result.Success,
            ["projectRoot"] = result.ProjectRoot,
            ["projectId"] = result.ProjectId,
            ["diagnostics"] = ToArray(result.Diagnostics)
        };

        return root.ToJsonString(Options);
    }

    public static string ForSessionStart(SessionStartResult result)
    {
        var root = new JsonObject
        {
            ["success"] = result.Success,
            ["sessionId"] = result.SessionId,
            ["projectId"] = result.ProjectId,
            ["startedAt"] = result.StartedAt,
            ["diagnostics"] = ToArray(result.Diagnostics)
        };

        return root.ToJsonString(Options);
    }

    public static string ForSessionClose(SessionCloseResult result)
    {
        var root = new JsonObject
        {
            ["success"] = result.Success,
            ["sessionId"] = result.SessionId,
            ["projectId"] = result.ProjectId,
            ["historyFile"] = result.HistoryFile,
            ["finishedAt"] = result.FinishedAt,
            ["diagnostics"] = ToArray(result.Diagnostics)
        };

        return root.ToJsonString(Options);
    }

    public static string ForContext(ProjectRuntimeContext context)
    {
        var sessions = new JsonArray();
        foreach (var session in context.OpenSessions)
        {
            sessions.Add(new JsonObject
            {
                ["sessionId"] = session.SessionId,
                ["startedAt"] = session.StartedAt,
                ["auraRole"] = session.AuraRole,
                ["objective"] = session.Objective
            });
        }

        var root = new JsonObject
        {
            ["project"] = new JsonObject
            {
                ["projectRoot"] = context.Project.ProjectRoot,
                ["projectId"] = context.Project.ProjectId,
                ["projectName"] = context.Project.ProjectName,
                ["valid"] = context.Project.IsValid
            },
            ["machine"] = context.Machine is null
                ? new JsonObject { ["machineId"] = null, ["machineName"] = null }
                : new JsonObject
                {
                    ["machineId"] = context.Machine.MachineId,
                    ["machineName"] = context.Machine.MachineName,
                    ["profilePath"] = context.Machine.ProfilePath
                },
            ["git"] = new JsonObject
            {
                ["available"] = context.Git.Availability != GitAvailability.Unavailable,
                ["repository"] = context.Git.Availability == GitAvailability.Available,
                ["branch"] = context.Git.Branch,
                ["commit"] = context.Git.Commit,
                ["workingTree"] = context.Git.WorkingTree.ToString().ToLowerInvariant(),
                ["mergeConflicts"] = context.Git.HasMergeConflicts
            },
            ["openSessions"] = sessions,
            ["diagnostics"] = ToArray(context.Diagnostics)
        };

        return root.ToJsonString(Options);
    }

    public static string ForSessionStatus(SessionStatusResult result)
    {
        var sessions = new JsonArray();
        foreach (var session in result.OpenSessions)
        {
            sessions.Add(new JsonObject
            {
                ["sessionId"] = session.SessionId,
                ["startedAt"] = session.StartedAt,
                ["auraRole"] = session.AuraRole,
                ["agent"] = session.Agent,
                ["model"] = session.Model,
                ["logicalUser"] = session.LogicalUser,
                ["objective"] = session.Objective
            });
        }

        var root = new JsonObject
        {
            ["success"] = result.Success,
            ["projectRoot"] = result.ProjectRoot,
            ["projectId"] = result.ProjectId,
            ["openSessions"] = sessions,
            ["diagnostics"] = ToArray(result.Diagnostics)
        };

        return root.ToJsonString(Options);
    }

    public static string ForAgentBootstrap(AgentBootstrapContext context)
    {
        var openSessions = new JsonArray();
        foreach (var session in context.Sessions.OpenSessions)
        {
            openSessions.Add(new JsonObject
            {
                ["sessionId"] = session.SessionId,
                ["startedAt"] = session.StartedAt,
                ["auraRole"] = session.AuraRole,
                ["objective"] = session.Objective
            });
        }

        var modules = new JsonArray();
        foreach (var module in context.ProjectMemory.Modules)
        {
            modules.Add(new JsonObject
            {
                ["name"] = module.Name,
                ["path"] = module.Path,
                ["present"] = module.Present
            });
        }

        var documentationMappings = new JsonArray();
        foreach (var mapping in context.Documentation.Mappings)
        {
            documentationMappings.Add(new JsonObject
            {
                ["change"] = mapping.Change,
                ["document"] = mapping.Document
            });
        }

        var root = new JsonObject
        {
            ["project"] = new JsonObject
            {
                ["valid"] = context.Project.Valid,
                ["projectRoot"] = context.Project.ProjectRoot,
                ["projectId"] = context.Project.ProjectId,
                ["projectName"] = context.Project.ProjectName
            },
            ["projectMemory"] = new JsonObject
            {
                ["version"] = context.ProjectMemory.Version,
                ["coreDocuments"] = ToJsonArray(context.ProjectMemory.CoreDocuments),
                ["modules"] = modules
            },
            ["machine"] = context.Machine is null
                ? new JsonObject { ["present"] = false, ["machineId"] = null, ["machineName"] = null }
                : new JsonObject
                {
                    ["present"] = context.Machine.Present,
                    ["machineId"] = context.Machine.MachineId,
                    ["machineName"] = context.Machine.MachineName,
                    ["profilePath"] = context.Machine.ProfilePath
                },
            ["git"] = new JsonObject
            {
                ["available"] = context.Git.Available,
                ["repository"] = context.Git.Repository,
                ["branch"] = context.Git.Branch,
                ["commit"] = context.Git.Commit,
                ["workingTree"] = context.Git.WorkingTree,
                ["mergeConflicts"] = context.Git.HasMergeConflicts
            },
            ["sessions"] = new JsonObject
            {
                ["hasOpenSession"] = context.Sessions.HasOpenSession,
                ["open"] = openSessions
            },
            ["agentContract"] = new JsonObject
            {
                ["sessionRequiredForRelevantWork"] = context.AgentContract.SessionRequiredForRelevantWork,
                ["historyAppendOnly"] = context.AgentContract.HistoryAppendOnly,
                ["updateCurrentStateWhenProjectStateChanges"] = context.AgentContract.UpdateCurrentStateWhenProjectStateChanges,
                ["relevantWorkExamples"] = ToJsonArray(context.AgentContract.RelevantWorkExamples),
                ["irrelevantWorkExamples"] = ToJsonArray(context.AgentContract.IrrelevantWorkExamples),
                ["handoffAllowed"] = context.AgentContract.HandoffAllowed
            },
            ["commands"] = new JsonObject
            {
                ["bootstrap"] = context.Commands.Bootstrap,
                ["start"] = context.Commands.Start,
                ["status"] = context.Commands.Status,
                ["close"] = context.Commands.Close
            },
            ["documentation"] = documentationMappings,
            ["diagnostics"] = ToArray(context.Diagnostics)
        };

        return root.ToJsonString(Options);
    }

    public static string ForManagerInspect(ProjectManagerInspection result) =>
        JsonSerializer.Serialize(result, Options);

    public static string ForManagerOperation<T>(ManagerOperationResult<T> result) =>
        JsonSerializer.Serialize(result, Options);

    public static string ForKnownProjects(IReadOnlyList<KnownProject> result) =>
        JsonSerializer.Serialize(result, Options);

    public static string ForHistory(IReadOnlyList<ManagerHistoryEntry> result) =>
        JsonSerializer.Serialize(result, Options);

    public static string ForHandoff(ProjectManagerHandoff result) =>
        JsonSerializer.Serialize(result, Options);

    public static string ForMachine(bool success, string? machineId, string? machineName, string profilePath,
        IEnumerable<Diagnostic> diagnostics)
    {
        var root = new JsonObject
        {
            ["success"] = success,
            ["machineId"] = machineId,
            ["machineName"] = machineName,
            ["profilePath"] = profilePath,
            ["diagnostics"] = ToArray(diagnostics)
        };

        return root.ToJsonString(Options);
    }

    private static JsonArray ToArray(IEnumerable<Diagnostic> diagnostics)
    {
        var array = new JsonArray();
        foreach (var diagnostic in diagnostics)
        {
            array.Add(new JsonObject
            {
                ["severity"] = diagnostic.Severity.ToString().ToUpperInvariant(),
                ["code"] = diagnostic.Code,
                ["message"] = diagnostic.Message,
                ["path"] = diagnostic.Path,
                ["suggestedAction"] = diagnostic.SuggestedAction
            });
        }

        return array;
    }

    private static JsonArray ToJsonArray(IEnumerable<string?> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add((JsonNode?)value);
        }

        return array;
    }
}
