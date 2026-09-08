using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class MachineValidateCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteMachineValidate();
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

        var result = MachineProfileValidator.Validate(path);

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForMachine(
                result.IsValid, result.MachineId, result.MachineName, result.ProfilePath, result.Diagnostics));
        }
        else
        {
            HumanReportWriter.WriteMachine(
                "AURA Machine Validate", result.IsValid, result.MachineId, result.MachineName, result.Diagnostics);
        }

        // An absent/invalid machine profile is informational for the overall tool (it never
        // blocks a project), but 'machine validate' is asked about the profile itself, so it
        // reports failure distinctly from a merely-absent profile.
        return result.IsValid ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
