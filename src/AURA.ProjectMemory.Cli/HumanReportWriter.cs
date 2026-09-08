using AURA.ProjectMemory.Agent;
using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Manager;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Cli;

/// <summary>Renders the default, human-readable console output.</summary>
internal static class HumanReportWriter
{
    public static void WriteValidation(ProjectValidationResult result)
    {
        Console.WriteLine("AURA Project Memory Validator");
        Console.WriteLine();

        if (result.ProjectRoot is not null)
        {
            Console.WriteLine("Project Root:");
            Console.WriteLine(result.ProjectRoot);
            Console.WriteLine();
        }

        if (result.ProjectId is not null)
        {
            Console.WriteLine("Project ID:");
            Console.WriteLine(result.ProjectId);
            Console.WriteLine();
        }

        Console.WriteLine("Status:");
        Console.WriteLine(result.IsValid ? "VALID" : "INVALID");
        Console.WriteLine();

        WriteSection("Errors", result.Errors, "x");
        WriteSection("Warnings", result.Warnings, "!");
        WriteSection("Info", result.Infos, "i");
    }

    public static void WriteInit(ProjectInitResult result)
    {
        Console.WriteLine("AURA Project Memory Init");
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine("Status:");
            Console.WriteLine("INITIALIZED");
            Console.WriteLine();
            Console.WriteLine("Project Root:");
            Console.WriteLine(result.ProjectRoot);
            Console.WriteLine();
            Console.WriteLine("Project ID:");
            Console.WriteLine(result.ProjectId);
            return;
        }

        Console.WriteLine("Status:");
        Console.WriteLine("FAILED");
        Console.WriteLine();
        WriteSection("Errors", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
    }

    public static void WriteSessionStart(SessionStartResult result)
    {
        Console.WriteLine("AURA Project Session Start");
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine("Status:");
            Console.WriteLine("STARTED");
            Console.WriteLine();
            Console.WriteLine("Session ID:");
            Console.WriteLine(result.SessionId);
            Console.WriteLine();
            Console.WriteLine("Started:");
            Console.WriteLine(result.StartedAt);
            Console.WriteLine();
            WriteSection("Info", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Info), "i");
            WriteSection("Warnings", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning), "!");
            return;
        }

        Console.WriteLine("Status:");
        Console.WriteLine("FAILED");
        Console.WriteLine();
        WriteSection("Errors", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
    }

    public static void WriteSessionClose(SessionCloseResult result)
    {
        Console.WriteLine("AURA Project Session Close");
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine("Status:");
            Console.WriteLine("CLOSED");
            Console.WriteLine();
            Console.WriteLine("History File:");
            Console.WriteLine(result.HistoryFile);
            Console.WriteLine();
            Console.WriteLine("Finished:");
            Console.WriteLine(result.FinishedAt);
            return;
        }

        Console.WriteLine("Status:");
        Console.WriteLine("FAILED");
        Console.WriteLine();
        WriteSection("Errors", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
    }

    public static void WriteSessionStatus(SessionStatusResult result)
    {
        Console.WriteLine("AURA Project Sessions");
        Console.WriteLine();

        if (!result.Success)
        {
            Console.WriteLine("Status:");
            Console.WriteLine("FAILED");
            Console.WriteLine();
            WriteSection("Errors", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
            return;
        }

        Console.WriteLine("Project Root:");
        Console.WriteLine(result.ProjectRoot);
        Console.WriteLine();

        if (result.OpenSessions.Count == 0)
        {
            Console.WriteLine("No open sessions.");
            return;
        }

        Console.WriteLine("OPEN");
        Console.WriteLine();
        foreach (var session in result.OpenSessions)
        {
            Console.WriteLine(session.SessionId);
            Console.WriteLine($"Started: {session.StartedAt}");
            if (session.Agent is not null)
            {
                Console.WriteLine($"Agent: {session.Agent}");
            }
            Console.WriteLine($"Role: {session.AuraRole}");
            Console.WriteLine($"Objective: {session.Objective}");
            Console.WriteLine();
        }
    }

    public static void WriteManagerInspect(ProjectManagerInspection result)
    {
        Console.WriteLine("AURA Project Manager Inspect");
        Console.WriteLine($"Project: {result.ProjectName ?? "(unknown)"}");
        Console.WriteLine($"Root: {result.ProjectRoot ?? "(unknown)"}");
        Console.WriteLine($"Project ID: {result.ProjectId ?? "(unknown)"}");
        Console.WriteLine($"Project Memory: {result.ProjectMemoryVersion ?? "(unknown)"}");
        Console.WriteLine($"Valid: {result.Valid}");
        Console.WriteLine($"Open sessions: {result.OpenSessions.Count}");
        Console.WriteLine($"History entries: {result.LastHistoryEntries.Count}");
        Console.WriteLine($"Git: {result.Git.Branch ?? "(unknown)"} / {result.Git.WorkingTree}");
        WriteManagerDocuments("Core documents", result.CoreDocuments);
        WriteManagerDocuments("Optional modules", result.OptionalModules);
        WriteSection("Warnings", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning), "!");
        WriteSection("Errors", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
    }

    public static void WriteManagerOperation<T>(string title, ManagerOperationResult<T> result)
    {
        Console.WriteLine(title);
        Console.WriteLine(result.Success ? "Status: OK" : "Status: FAILED");
        if (result.Value is not null) Console.WriteLine(result.Value);
        WriteSection("Errors", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
    }

    public static void WriteKnownProjects(IReadOnlyList<KnownProject> projects)
    {
        Console.WriteLine("AURA Project Manager Known Projects");
        if (projects.Count == 0)
        {
            Console.WriteLine("No remembered projects.");
            return;
        }

        foreach (var project in projects)
        {
            Console.WriteLine($"- {project.Entry.Alias ?? project.Entry.ProjectId}: {project.Entry.ProjectRoot}");
            Console.WriteLine($"  Exists: {project.PathExists}; Valid: {project.Valid}");
        }
    }

    public static void WriteHistory(IReadOnlyList<ManagerHistoryEntry> entries)
    {
        Console.WriteLine("AURA Project Manager History");
        foreach (var entry in entries)
        {
            Console.WriteLine($"- {entry.FileName}");
            if (entry.Summary is not null) Console.WriteLine($"  {entry.Summary}");
        }
    }

    public static void WriteHandoff(ProjectManagerHandoff result)
    {
        Console.WriteLine("AURA Project Manager Handoff");
        Console.WriteLine($"Project: {result.ProjectName ?? "(unknown)"}");
        Console.WriteLine($"Project ID: {result.ProjectId ?? "(unknown)"}");
        Console.WriteLine($"Valid: {result.Valid}");
        if (result.CurrentStateSummary is not null) Console.WriteLine(result.CurrentStateSummary);
        Console.WriteLine($"Open sessions: {result.OpenSessions.Count}");
        Console.WriteLine($"Bootstrap: {result.RecommendedBootstrapCommand}");
        WriteSection("Warnings", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning), "!");
        WriteSection("Errors", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
    }

    private static void WriteManagerDocuments(string title, IReadOnlyList<ManagerDocument> documents)
    {
        Console.WriteLine($"{title}:");
        foreach (var document in documents)
        {
            Console.WriteLine($"  - {document.Name}: {document.Path} ({(document.Present ? "present" : "absent")})");
        }
    }

    public static void WriteMachine(string title, bool success, string? machineId, string? machineName,
        IEnumerable<Diagnostic> diagnostics)
    {
        Console.WriteLine(title);
        Console.WriteLine();
        Console.WriteLine("Status:");
        Console.WriteLine(success ? "OK" : "FAILED");
        Console.WriteLine();

        if (machineId is not null)
        {
            Console.WriteLine("Machine ID:");
            Console.WriteLine(machineId);
            Console.WriteLine();
        }

        if (machineName is not null)
        {
            Console.WriteLine("Machine Name:");
            Console.WriteLine(machineName);
            Console.WriteLine();
        }

        var list = diagnostics.ToList();
        WriteSection("Errors", list.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
        WriteSection("Warnings", list.Where(d => d.Severity == DiagnosticSeverity.Warning), "!");
        WriteSection("Info", list.Where(d => d.Severity == DiagnosticSeverity.Info), "i");
    }

    public static void WriteContext(ProjectRuntimeContext context)
    {
        Console.WriteLine("AURA Project Context");
        Console.WriteLine();

        var p = context.Project;
        Console.WriteLine("Project:");
        Console.WriteLine($"  Root: {p.ProjectRoot ?? "(unknown)"}");
        Console.WriteLine($"  ID:   {p.ProjectId ?? "(unknown)"}");
        Console.WriteLine($"  Name: {p.ProjectName ?? "(unknown)"}");
        Console.WriteLine($"  Valid: {p.IsValid}");
        Console.WriteLine();

        if (context.Machine is not null)
        {
            Console.WriteLine("Machine:");
            Console.WriteLine($"  ID:   {context.Machine.MachineId ?? "(unknown)"}");
            Console.WriteLine($"  Name: {context.Machine.MachineName ?? "(unknown)"}");
            Console.WriteLine();
        }

        Console.WriteLine("Git:");
        Console.WriteLine($"  Available: {context.Git.Availability}");
        Console.WriteLine($"  Repository: {context.Git.Availability == AURA.ProjectMemory.Git.GitAvailability.Available}");
        Console.WriteLine($"  Branch: {context.Git.Branch ?? "(detached or unknown)"}");
        Console.WriteLine($"  Commit: {context.Git.Commit ?? "(unknown)"}");
        Console.WriteLine($"  Working tree: {context.Git.WorkingTree}");
        if (context.Git.HasMergeConflicts)
        {
            Console.WriteLine("  Merge conflicts: true");
        }
        Console.WriteLine();

        Console.WriteLine($"Open sessions on this machine: {context.OpenSessions.Count}");
        foreach (var session in context.OpenSessions)
        {
            Console.WriteLine($"  - {session.SessionId} ({session.AuraRole})");
        }

        WriteSection("Warnings", context.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning), "!");
        WriteSection("Errors", context.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
    }

    public static void WriteAgentBootstrap(AgentBootstrapContext context)
    {
        Console.WriteLine("AURA Agent Bootstrap");
        Console.WriteLine();

        Console.WriteLine($"Project valid: {context.Project.Valid}");
        Console.WriteLine($"  Root: {context.Project.ProjectRoot ?? "(unknown)"}");
        Console.WriteLine($"  ID:   {context.Project.ProjectId ?? "(unknown)"}");
        Console.WriteLine($"  Name: {context.Project.ProjectName ?? "(unknown)"}");
        Console.WriteLine();

        Console.WriteLine($"Project Memory version: {context.ProjectMemory.Version}");
        Console.WriteLine("Core documents to read:");
        foreach (var doc in context.ProjectMemory.CoreDocuments)
        {
            Console.WriteLine($"  - {doc}");
        }

        if (context.ProjectMemory.Modules.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Optional modules:");
            foreach (var module in context.ProjectMemory.Modules)
            {
                var status = module.Present ? "present" : "absent";
                Console.WriteLine($"  - {module.Name} ({status}): {module.Path}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Machine: {(context.Machine?.Present == true ? context.Machine.MachineName ?? "(unnamed)" : "unavailable")}");
        if (context.Machine?.Present == true)
        {
            Console.WriteLine($"  ID: {context.Machine.MachineId}");
            Console.WriteLine($"  Profile: {context.Machine.ProfilePath}");
        }

        Console.WriteLine();
        Console.WriteLine("Git:");
        Console.WriteLine($"  Available: {context.Git.Available}");
        Console.WriteLine($"  Repository: {context.Git.Repository}");
        Console.WriteLine($"  Branch: {context.Git.Branch ?? "(detached or unknown)"}");
        Console.WriteLine($"  Commit: {context.Git.Commit ?? "(unknown)"}");
        Console.WriteLine($"  Working tree: {context.Git.WorkingTree}");
        if (context.Git.HasMergeConflicts)
        {
            Console.WriteLine("  Merge conflicts: true");
        }

        Console.WriteLine();
        Console.WriteLine($"Open sessions on this machine: {context.Sessions.OpenSessions.Count}");
        foreach (var session in context.Sessions.OpenSessions)
        {
            Console.WriteLine($"  - {session.SessionId} ({session.AuraRole})");
        }

        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine($"  bootstrap: {context.Commands.Bootstrap}");
        Console.WriteLine($"  start:     {context.Commands.Start}");
        Console.WriteLine($"  status:    {context.Commands.Status}");
        Console.WriteLine($"  close:     {context.Commands.Close}");

        Console.WriteLine();
        Console.WriteLine("Relevant work examples (start a session before persistent changes):");
        foreach (var example in context.AgentContract.RelevantWorkExamples)
        {
            Console.WriteLine($"  - {example}");
        }

        Console.WriteLine();
        Console.WriteLine("Update documentation as follows:");
        foreach (var mapping in context.Documentation.Mappings)
        {
            Console.WriteLine($"  {mapping.Change} -> {mapping.Document}");
        }

        WriteSection("Warnings", context.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning), "!");
        WriteSection("Errors", context.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), "x");
        WriteSection("Info", context.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Info), "i");
    }

    private static void WriteSection(string title, IEnumerable<Diagnostic> diagnostics, string marker)
    {
        var list = diagnostics.ToList();
        if (list.Count == 0)
        {
            return;
        }

        Console.WriteLine(title);
        foreach (var diagnostic in list)
        {
            var location = diagnostic.Path is null ? string.Empty : $" ({diagnostic.Path})";
            Console.WriteLine($"{marker} [{diagnostic.Code}] {diagnostic.Message}{location}");
        }

        Console.WriteLine();
    }
}
