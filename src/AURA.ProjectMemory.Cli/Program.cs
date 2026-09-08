using AURA.ProjectMemory.Cli;
using AURA.ProjectMemory.Cli.Commands;

var arguments = args.ToList();

if (arguments.Count == 0 || ArgParsing.IsHelp(arguments[0]))
{
    HelpText.WriteRoot();
    return ExitCode.Success;
}

return arguments[0] switch
{
    "project" => RunProject(arguments.Skip(1).ToList()),
    "machine" => RunMachine(arguments.Skip(1).ToList()),
    "agent" => RunAgent(arguments.Skip(1).ToList()),
    "manager" => RunManager(arguments.Skip(1).ToList()),
    _ => Unknown(arguments[0])
};

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    HelpText.WriteRoot();
    return ExitCode.InvalidArguments;
}

static int RunProject(List<string> arguments)
{
    if (arguments.Count == 0 || ArgParsing.IsHelp(arguments[0]))
    {
        HelpText.WriteProject();
        return ExitCode.Success;
    }

    var subcommand = arguments[0];
    var rest = arguments.Skip(1).ToList();

    if (subcommand == "session")
    {
        return RunSession(rest);
    }

    return subcommand switch
    {
        "init" => ProjectInitCommand.Run(rest),
        "validate" => ProjectValidateCommand.Run(rest),
        "context" => ProjectContextCommand.Run(rest),
        _ => UnknownSubcommand("project", subcommand, HelpText.WriteProject)
    };
}

static int RunSession(List<string> arguments)
{
    if (arguments.Count == 0 || ArgParsing.IsHelp(arguments[0]))
    {
        Console.WriteLine("aura project session - start, close, and inspect Project Memory sessions");
        Console.WriteLine();
        Console.WriteLine("Subcommands: start, close, status");
        Console.WriteLine("Run 'aura project session <subcommand> --help' for details.");
        return ExitCode.Success;
    }

    var subcommand = arguments[0];
    var rest = arguments.Skip(1).ToList();

    return subcommand switch
    {
        "start" => ProjectSessionStartCommand.Run(rest),
        "close" => ProjectSessionCloseCommand.Run(rest),
        "status" => ProjectSessionStatusCommand.Run(rest),
        _ => UnknownSubcommand("project session", subcommand, () => { })
    };
}

static int RunAgent(List<string> arguments)
{
    if (arguments.Count == 0 || ArgParsing.IsHelp(arguments[0]))
    {
        HelpText.WriteAgent();
        return ExitCode.Success;
    }

    var subcommand = arguments[0];
    var rest = arguments.Skip(1).ToList();

    return subcommand switch
    {
        "bootstrap" => AgentBootstrapCommand.Run(rest),
        _ => UnknownSubcommand("agent", subcommand, HelpText.WriteAgent)
    };
}

static int RunManager(List<string> arguments)
{
    if (arguments.Count == 0 || ArgParsing.IsHelp(arguments[0]))
    {
        HelpText.WriteManager();
        return ExitCode.Success;
    }

    var subcommand = arguments[0];
    var rest = arguments.Skip(1).ToList();

    return subcommand switch
    {
        "inspect" => ManagerInspectCommand.Run(rest),
        "remember" => ManagerRememberCommand.Run(rest),
        "list-known" => ManagerListKnownCommand.Run(rest),
        "forget" => ManagerForgetCommand.Run(rest),
        "history" => ManagerHistoryCommand.Run(rest),
        "handoff" => ManagerHandoffCommand.Run(rest),
        _ => UnknownSubcommand("manager", subcommand, HelpText.WriteManager)
    };
}

static int RunMachine(List<string> arguments)
{
    if (arguments.Count == 0 || ArgParsing.IsHelp(arguments[0]))
    {
        HelpText.WriteMachine();
        return ExitCode.Success;
    }

    var subcommand = arguments[0];
    var rest = arguments.Skip(1).ToList();

    return subcommand switch
    {
        "init" => MachineInitCommand.Run(rest),
        "validate" => MachineValidateCommand.Run(rest),
        _ => UnknownSubcommand("machine", subcommand, HelpText.WriteMachine)
    };
}

static int UnknownSubcommand(string parent, string subcommand, Action writeHelp)
{
    Console.Error.WriteLine($"Unknown subcommand '{parent} {subcommand}'.");
    writeHelp();
    return ExitCode.InvalidArguments;
}
