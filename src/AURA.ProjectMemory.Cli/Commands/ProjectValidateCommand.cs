using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Cli.Commands;

internal static class ProjectValidateCommand
{
    public static int Run(List<string> rest)
    {
        if (rest.Any(ArgParsing.IsHelp))
        {
            HelpText.WriteValidate();
            return ExitCode.Success;
        }

        string? path = null;
        var json = false;

        foreach (var token in rest)
        {
            if (token == "--json")
            {
                json = true;
            }
            else if (path is null && !token.StartsWith('-'))
            {
                path = token;
            }
            else
            {
                Console.Error.WriteLine($"Unrecognized argument '{token}'.");
                return ExitCode.InvalidArguments;
            }
        }

        var result = ProjectMemoryValidator.Validate(path ?? Directory.GetCurrentDirectory());

        if (json)
        {
            Console.WriteLine(JsonReportWriter.ForValidation(result));
        }
        else
        {
            HumanReportWriter.WriteValidation(result);
        }

        return result.IsValid ? ExitCode.Success : ExitCode.OperationFailed;
    }
}
