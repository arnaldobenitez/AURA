using AURA.ProjectMemory.Session;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ProjectSessionStartCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteSessionStart();
            return ExitCode.Success;
        }

        string? path = null;
        string? objective = null;
        string? role = null;
        string? user = null;
        string? agent = null;
        string? model = null;
        string? slug = null;
        string? machinePath = null;
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
                case "--objective":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--objective", out objective)) return ExitCode.InvalidArguments;
                    break;
                case "--role":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--role", out role)) return ExitCode.InvalidArguments;
                    break;
                case "--user":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--user", out user)) return ExitCode.InvalidArguments;
                    break;
                case "--agent":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--agent", out agent)) return ExitCode.InvalidArguments;
                    break;
                case "--model":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--model", out model)) return ExitCode.InvalidArguments;
                    break;
                case "--slug":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--slug", out slug)) return ExitCode.InvalidArguments;
                    break;
                case "--machine-path":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--machine-path", out machinePath)) return ExitCode.InvalidArguments;
                    break;
                default:
                    Console.Error.WriteLine($"Unrecognized argument '{token}'.");
                    return ExitCode.InvalidArguments;
            }
        }

        if (string.IsNullOrWhiteSpace(objective) || string.IsNullOrWhiteSpace(role))
        {
            Console.Error.WriteLine("--objective and --role are required.");
            return ExitCode.InvalidArguments;
        }

        var request = new SessionStartRequest(objective, role, user, agent, model, slug, machinePath);
        var result = SessionManager.Start(path ?? Directory.GetCurrentDirectory(), request);

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForSessionStart(result));
        }
        else
        {
            HumanReportWriter.WriteSessionStart(result);
        }

        return result.Success ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
