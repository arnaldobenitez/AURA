using AURA.ProjectMemory.Manager;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ManagerHistoryCommand
{
    public static int Run(List<string> arguments)
    {
        var options = ManagerCommandParsing.Parse(arguments, HelpText.WriteManagerHistory);
        if (options is null) return ExitCode.InvalidArguments;

        var result = ProjectManagerService.History(options.Path, options.Take);
        if (options.Json)
            Console.WriteLine(JsonReportWriter.ForHistory(result));
        else
            HumanReportWriter.WriteHistory(result);

        return ExitCode.Success;
    }
}
