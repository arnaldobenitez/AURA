using AURA.ProjectMemory.Manager;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ManagerInspectCommand
{
    public static int Run(List<string> arguments)
    {
        var options = ManagerCommandParsing.Parse(arguments, HelpText.WriteManagerInspect);
        if (options is null) return ExitCode.InvalidArguments;

        var result = ProjectManagerService.Inspect(options.Path, options.Take, options.MachinePath);
        if (options.Json)
            Console.WriteLine(JsonReportWriter.ForManagerInspect(result));
        else
            HumanReportWriter.WriteManagerInspect(result);

        return ExitCode.Success;
    }
}
