using AURA.ProjectMemory.Manager;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ManagerListKnownCommand
{
    public static int Run(List<string> arguments)
    {
        var options = ManagerCommandParsing.Parse(arguments, HelpText.WriteManagerListKnown, allowIndexPath: true);
        if (options is null) return ExitCode.InvalidArguments;

        var result = ProjectManagerService.ListKnown(options.IndexPath);
        if (options.Json)
            Console.WriteLine(JsonReportWriter.ForKnownProjects(result));
        else
            HumanReportWriter.WriteKnownProjects(result);

        return ExitCode.Success;
    }
}
