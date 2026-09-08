using AURA.ProjectMemory.Manager;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ManagerHandoffCommand
{
    public static int Run(List<string> arguments)
    {
        var options = ManagerCommandParsing.Parse(arguments, HelpText.WriteManagerHandoff);
        if (options is null) return ExitCode.InvalidArguments;

        var result = ProjectManagerService.Handoff(options.Path, options.Take, options.MachinePath);
        if (options.Json)
            Console.WriteLine(JsonReportWriter.ForHandoff(result));
        else
            HumanReportWriter.WriteHandoff(result);

        return ExitCode.Success;
    }
}
