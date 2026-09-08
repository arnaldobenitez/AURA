namespace AURA.ProjectMemory.Cli;

internal static class ArgParsing
{
    public static bool IsHelp(string token) => token is "--help" or "-h";

    public static bool TryTakeValue(List<string> args, ref int i, string optionName, out string? value)
    {
        if (i + 1 >= args.Count)
        {
            Console.Error.WriteLine($"{optionName} requires a value.");
            value = null;
            return false;
        }

        value = args[++i];
        return true;
    }
}
