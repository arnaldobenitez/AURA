using AURA.ProjectMemory.Manager;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ManagerRememberCommand
{
    public static int Run(List<string> arguments)
    {
        var options = ManagerCommandParsing.Parse(arguments, HelpText.WriteManagerRemember, allowAlias: true,
            allowIndexPath: true);
        if (options is null) return ExitCode.InvalidArguments;

        var result = ProjectManagerService.Remember(options.Path, options.Alias, options.IndexPath);
        if (options.Json)
            Console.WriteLine(JsonReportWriter.ForManagerOperation(result));
        else
            HumanReportWriter.WriteManagerOperation("AURA Project Manager Remember", result);

        return result.Success ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
