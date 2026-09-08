using AURA.ProjectMemory.Session;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ProjectSessionStatusCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteSessionStatus();
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

        var result = SessionManager.Status(path ?? Directory.GetCurrentDirectory());

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForSessionStatus(result));
        }
        else
        {
            HumanReportWriter.WriteSessionStatus(result);
        }

        return result.Success ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
