using AURA.ProjectMemory.Agent;

namespace AURA.ProjectMemory.Cli.Commands;

/// <summary>
/// Executes <c>aura agent bootstrap</c>. This is the universal entry point for any AI agent:
/// it returns structured context without modifying the project. Exit codes are strict per
/// AURA_AGENT_CONTRACT v1.0 §28: 0 = valid, 1 = invalid Project Memory, 2 = bad arguments.
/// </summary>
internal static class AgentBootstrapCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteAgentBootstrap();
            return ExitCode.Success;
        }

        string? path = null;
        string? machinePath = null;
        var json = false;

        for (var i = 0; i < rest.Count; i++)
        {
            var token = rest[i];
            if (ArgParsing.IsHelp(token))
            {
                HelpText.WriteAgentBootstrap();
                return ExitCode.Success;
            }

            if (token == "--json")
            {
                json = true;
            }
            else if (token == "--path")
            {
                if (!ArgParsing.TryTakeValue(rest, ref i, "--path", out path)) return ExitCode.InvalidArguments;
            }
            else if (token == "--machine-path")
            {
                if (!ArgParsing.TryTakeValue(rest, ref i, "--machine-path", out machinePath)) return ExitCode.InvalidArguments;
            }
            else if (!token.StartsWith("-"))
            {
                path = token;
            }
            else
            {
                Console.Error.WriteLine($"Unrecognized argument '{token}'.");
                return ExitCode.InvalidArguments;
            }
        }

        var context = AgentBootstrapService.Bootstrap(path ?? Directory.GetCurrentDirectory(), machinePath);

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForAgentBootstrap(context));
        }
        else
        {
            HumanReportWriter.WriteAgentBootstrap(context);
        }

        return context.Project.Valid ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
