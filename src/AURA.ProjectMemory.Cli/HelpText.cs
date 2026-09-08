namespace AURA.ProjectMemory.Cli;

internal static class HelpText
{
    public static void WriteRoot()
    {
        Console.WriteLine("""
            AURA Project Memory tooling

            Usage:
              aura project init [<path>] [--name <name>] [--json]
              aura project validate [<path>] [--json]
              aura project context [<path>] [--json]
              aura project session start --objective <text> --role <role> [options]
              aura project session close --session <uuid> (--input <file> | --stdin) [options]
              aura project session status [<path>] [--json]
              aura machine init [--path <file>] [--json]
              aura machine validate [--path <file>] [--json]
              aura agent bootstrap [<path>] [--json] [--machine-path <file>]
              aura manager <inspect|remember|list-known|forget|history|handoff> [options]

            Run 'aura project --help', 'aura machine --help', 'aura agent --help' or
            'aura manager --help' for details.
            """);
    }

    public static void WriteManager()
    {
        Console.WriteLine("""
            aura manager - inspect and operate known AURA Project Memory projects

            Subcommands:
              inspect       Show project, Core, modules, machine, Git, sessions and recent history.
              remember      Add or update a project in the local ~/.aura/projects/index.json.
              list-known    List locally remembered projects and current validity.
              forget        Remove a remembered project by alias or projectId.
              history       Show recent semantic history entries.
              handoff       Produce a compact continuity view for another agent.

            Run 'aura manager <subcommand> --help' for details.
            """);
    }

    public static void WriteManagerInspect() => WriteManagerSubcommand("inspect", "aura manager inspect [<path>] [--take <n>] [--json]",
        "Read-only complete inspection. It never creates sessions or changes project files.");

    public static void WriteManagerRemember() => WriteManagerSubcommand("remember", "aura manager remember [<path>] [--alias <name>] [--json]",
        "Validates and remembers a project in the user-local index. It does not modify the project.");

    public static void WriteManagerListKnown() => WriteManagerSubcommand("list-known", "aura manager list-known [--json]",
        "Lists the user-local index, including whether each remembered path exists and validates.");

    public static void WriteManagerForget() => WriteManagerSubcommand("forget", "aura manager forget <alias-or-projectId> [--json]",
        "Removes only the local index entry; it never deletes project files or history.");

    public static void WriteManagerHistory() => WriteManagerSubcommand("history", "aura manager history [<path>] [--take <n>] [--json]",
        "Reads recent Markdown records from the declared docs/historial directory, newest first.");

    public static void WriteManagerHandoff() => WriteManagerSubcommand("handoff", "aura manager handoff [<path>] [--take <n>] [--json]",
        "Creates a compact, read-only continuity view for another agent.");

    private static void WriteManagerSubcommand(string command, string usage, string description)
    {
        Console.WriteLine($"""
            {usage}

            {description}

            Options:
              --take <n>        Number of history entries (default: 5).
              --machine-path    Override the default ~/.aura/machine.json profile.
              --index-path <f>  Override the local index path (useful for isolated tooling/tests).
              --json             Emit machine-readable JSON.
            """);
    }

    public static void WriteAgent()
    {
        Console.WriteLine("""
            aura agent - agent-neutral bootstrap commands for AURA Project Memory

            Subcommands:
              bootstrap   Return structured context for an agent to work correctly
                            (project, Core documents, modules, machine, Git, open sessions,
                            lifecycle commands, and documentation policy).

            Run 'aura agent bootstrap --help' for details.
            """);
    }

    public static void WriteProject()
    {
        Console.WriteLine("""
            aura project - initialize, validate, inspect context, and manage sessions of an AURA Project Memory Core

            Subcommands:
              init             Initialize the Project Memory Core on a directory.
              validate         Validate an existing project against AURA_PROJECT_MEMORY v1.0.
              context          Show project, machine, Git, and open-session context.
              session start    Open a session (AURA_PROJECT_MEMORY v1.0 §9.1).
              session close    Close a session and append a semantic record to docs/historial/.
              session status   List sessions currently open on this project.

            Run 'aura project <subcommand> --help' for details.
            """);
    }

    public static void WriteMachine()
    {
        Console.WriteLine("""
            aura machine - manage the local, out-of-project machine profile (~/.aura/machine.json)

            Subcommands:
              init       Create the local machine profile (never overwrites an existing one).
              validate   Validate the local machine profile against machine.schema.json.

            Run 'aura machine <subcommand> --help' for details.
            """);
    }

    public static void WriteInit()
    {
        Console.WriteLine("""
            aura project init [<path>] [--name <name>] [--json]

            Initializes AURA.md, .aura/project.json, docs/00-PROYECTO.md, docs/01-ESTADO-ACTUAL.md
            and docs/historial/ on <path> (default: current directory). Never overwrites an
            existing installation and never creates optional modules.

            Options:
              --name <name>   projectName to record in .aura/project.json (default: directory name).
              --json          Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = initialized, 1 = already initialized or write failure, 2 = bad arguments.
            """);
    }

    public static void WriteContext()
    {
        Console.WriteLine("""
            aura project context [<path>] [--json]

            Displays the unified runtime context of the project: PROJECT_ROOT, projectId,
            local machine profile, Git context (when available), and sessions currently open on
            this machine. Never modifies the project.

            Options:
              --machine-path   Override the default ~/.aura/machine.json machine profile.
              --json           Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = displayed, 2 = bad arguments.
            """);
    }

    public static void WriteValidate()
    {
        Console.WriteLine("""
            aura project validate [<path>] [--json]

            Resolves PROJECT_ROOT from <path> (default: current directory) by walking up to the
            nearest .aura/project.json, then validates the Project Memory Core against
            AURA_PROJECT_MEMORY v1.0 and its JSON Schemas. Never modifies the project.

            Options:
              --json   Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = valid, 1 = invalid, 2 = bad arguments.
            """);
    }

    public static void WriteSessionStart()
    {
        Console.WriteLine("""
            aura project session start --objective <text> --role <role> [options]

            Opens a session on the project resolved from --path (default: current directory).
            Requires the project's Core to already be valid (reuses 'project validate' internally).
            Generates sessionId (UUID v4) and startedAt automatically; captures the local machine
            profile when 'aura machine init' has been run, but never fails just because it hasn't.

            Options:
              --path <dir>       Directory to resolve PROJECT_ROOT from (default: current directory).
              --objective <text> What this session intends to do. Required.
              --role <role>      auraRole for this session (e.g. coding-agent, analyst). Required.
              --user <name>      logicalUser responsible for the session.
              --agent <name>     Agent or tool executing the session.
              --model <name>     Real model used, if known.
              --slug <text>      Optional short slug reused in the eventual history filename.
              --machine-path     Override the default ~/.aura/machine.json machine profile.
              --json             Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = started, 1 = invalid project, 2 = bad arguments.
            """);
    }

    public static void WriteSessionClose()
    {
        Console.WriteLine("""
            aura project session close --session <uuid> (--input <file> | --stdin) [options]

            Closes an open session and appends exactly one semantic record to the project's
            declared history directory, named per AURA_PROJECT_MEMORY v1.0 §11. Never overwrites
            an existing history file (append-only) and never touches a session opened on a
            different project.

            Options:
              --path <dir>     Directory to resolve PROJECT_ROOT from (default: current directory).
              --session <uuid> sessionId returned by 'session start'. Required.
              --input <file>   JSON file with the close payload (result/pending/nextRecommendedStep
                                are mandatory; workPerformed, modifiedFiles, decisions, tests,
                                problems, technicalDebt, updatedDocuments, and optional Git fields
                                are all optional).
              --stdin          Read the same JSON payload from standard input instead of a file.
              --json           Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = closed, 1 = not found/already closed/mismatch/invalid payload/collision,
            2 = bad arguments.
            """);
    }

    public static void WriteSessionStatus()
    {
        Console.WriteLine("""
            aura project session status [<path>] [--json]

            Lists sessions currently open (not yet closed) on the project resolved from <path>
            (default: current directory). Read-only; never modifies the project.

            Options:
              --json   Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = listed (even if empty), 1 = project not found, 2 = bad arguments.
            """);
    }

    public static void WriteMachineInit()
    {
        Console.WriteLine("""
            aura machine init [--path <file>] [--json]

            Creates the local machine profile (default: ~/.aura/machine.json) with a fresh
            machineId (UUID v4) and the real machine name. Never writes logicalUser, osUser, or
            any project path, and never overwrites an existing profile.

            Options:
              --path <file>   Override the default profile location.
              --json          Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = created, 1 = already initialized/write failure, 2 = bad arguments.
            """);
    }

    public static void WriteMachineValidate()
    {
        Console.WriteLine("""
            aura machine validate [--path <file>] [--json]

            Validates the local machine profile against machine.schema.json.

            Options:
              --path <file>   Override the default profile location.
              --json          Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = valid, 1 = absent or invalid, 2 = bad arguments.
            """);
    }

    public static void WriteAgentBootstrap()
    {
        Console.WriteLine("""
            aura agent bootstrap [<path>] [--json] [--machine-path <file>]

            Returns the universal, provider-neutral bootstrap context for an AI agent:
            PROJECT_ROOT, projectId, Project Memory version, Core documents, optional modules,
            machine profile, Git context, open sessions, session lifecycle commands, and
            documentation-update policy. Never modifies the project.

            Options:
              --path           Directory to resolve PROJECT_ROOT from (default: current directory).
              --machine-path   Override the default ~/.aura/machine.json machine profile.
              --json           Emit a machine-readable JSON report instead of human-readable text.

            Exit codes: 0 = Project Memory valid, 1 = invalid, 2 = bad arguments.
            """);
    }
}
