using AURA.ProjectMemory.Initialization;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ProjectInitCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteInit();
            return ExitCode.Success;
        }

        string? path = null;
        string? name = null;
        var json = false;

        for (var i = 0; i < rest.Count; i++)
        {
            switch (rest[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--name":
                    if (!ArgParsing.TryTakeValue(rest, ref i, "--name", out name))
                    {
                        return ExitCode.InvalidArguments;
                    }
                    break;
                default:
                    if (path is not null || rest[i].StartsWith('-'))
                    {
                        Console.Error.WriteLine($"Unrecognized argument '{rest[i]}'.");
                        return ExitCode.InvalidArguments;
                    }
                    path = rest[i];
                    break;
            }
        }

        var result = ProjectMemoryInitializer.Init(path ?? Directory.GetCurrentDirectory(), name);

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForInit(result));
        }
        else
        {
            HumanReportWriter.WriteInit(result);
        }

        return result.Success ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
