using AURA.ProjectMemory.Session;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ProjectSessionCloseCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteSessionClose();
            return ExitCode.Success;
        }

        string? path = null;
        string? sessionId = null;
        string? inputFile = null;
        var useStdin = false;
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
                case "--session":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--session", out sessionId)) return ExitCode.InvalidArguments;
                    break;
                case "--input":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--input", out inputFile)) return ExitCode.InvalidArguments;
                    break;
                case "--stdin":
                    useStdin = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unrecognized argument '{token}'.");
                    return ExitCode.InvalidArguments;
            }
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Console.Error.WriteLine("--session <uuid> is required.");
            return ExitCode.InvalidArguments;
        }

        if (inputFile is null && !useStdin)
        {
            Console.Error.WriteLine("Provide --input <file> or --stdin with the close payload.");
            return ExitCode.InvalidArguments;
        }

        string rawJson;
        try
        {
            rawJson = useStdin ? Console.In.ReadToEnd() : File.ReadAllText(inputFile!);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Could not read the close payload: {ex.Message}");
            return ExitCode.InvalidArguments;
        }

        SessionCloseInput? input;
        try
        {
            input = SessionCloseInput.Parse(rawJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.Error.WriteLine($"The close payload is not valid JSON: {ex.Message}");
            return ExitCode.InvalidArguments;
        }

        if (input is null)
        {
            Console.Error.WriteLine("The close payload is empty.");
            return ExitCode.InvalidArguments;
        }

        var result = SessionManager.Close(path ?? Directory.GetCurrentDirectory(), sessionId, input.ToRequest());

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForSessionClose(result));
        }
        else
        {
            HumanReportWriter.WriteSessionClose(result);
        }

        return result.Success ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
