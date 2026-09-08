using AURA.ProjectMemory.Manager;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ManagerForgetCommand
{
    public static int Run(List<string> arguments)
    {
        var options = ManagerCommandParsing.Parse(arguments, HelpText.WriteManagerForget, allowIndexPath: true,
            allowSelector: true);
        if (options is null || string.IsNullOrWhiteSpace(options.Selector))
        {
            Console.Error.WriteLine("A projectId or alias is required.");
            return ExitCode.InvalidArguments;
        }

        var result = ProjectManagerService.Forget(options.Selector, options.IndexPath);
        if (options.Json)
            Console.WriteLine(JsonReportWriter.ForManagerOperation(result));
        else
            HumanReportWriter.WriteManagerOperation("AURA Project Manager Forget", result);

        return result.Success ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
