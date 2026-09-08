using AURA.ProjectMemory.Cli;
using AURA.ProjectMemory.Git;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ProjectContextCommand
{
    public static int Run(List<string> arguments)
    {
        string path = Environment.CurrentDirectory;
        string? machinePath = null;
        bool json = false;

        for (var i = 0; i < arguments.Count; i++)
        {
            var arg = arguments[i];
            if (ArgParsing.IsHelp(arg))
            {
                HelpText.WriteContext();
                return ExitCode.Success;
            }

            if (arg == "--json")
            {
                json = true;
            }
            else if (arg == "--machine-path")
            {
                if (!ArgParsing.TryTakeValue(arguments, ref i, "--machine-path", out machinePath)) return ExitCode.InvalidArguments;
            }
            else if (arg == "--path")
            {
                if (!ArgParsing.TryTakeValue(arguments, ref i, "--path", out var value)) return ExitCode.InvalidArguments;
                path = value!;
            }
            else if (!arg.StartsWith("-"))
            {
                path = arg;
            }
            else
            {
                Console.Error.WriteLine($"Unknown option '{arg}'.");
                HelpText.WriteContext();
                return ExitCode.InvalidArguments;
            }
        }

        var context = ProjectContextService.Get(path, machineProfilePath: machinePath);

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForContext(context));
        }
        else
        {
            HumanReportWriter.WriteContext(context);
        }

        // Context is informational: success means we were able to look. Validation errors are part
        // of the report, not a CLI failure, because an agent may still use the discovered root/id.
        return ExitCode.Success;
    }
}
