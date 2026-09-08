using AURA.ProjectMemory.Initialization;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class MachineInitCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteMachineInit();
            return ExitCode.Success;
        }

        string? path = null;
        var json = false;

        for (var i = 0; i < rest.Count; i++)
        {
            var token = rest[i];
            switch (token)
            {
                case "--json":
                    json = true;
                    break;
                case "--path":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--path", out path)) return ExitCode.InvalidArguments;
                    break;
                default:
                    Console.Error.WriteLine($"Unrecognized argument '{token}'.");
                    return ExitCode.InvalidArguments;
            }
        }

        var result = MachineProfileInitializer.Init(path);

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForMachine(result.Success, result.MachineId, result.MachineName,
                path ?? AURA.ProjectMemory.Validation.MachineProfileValidator.DefaultPath(), result.Diagnostics));
        }
        else
        {
            HumanReportWriter.WriteMachine("AURA Machine Init", result.Success, result.MachineId, result.MachineName,
                result.Diagnostics);
        }

        return result.Success ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
