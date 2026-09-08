namespace AURA.ProjectMemory.Cli.Commands;

internal sealed record ManagerCommandOptions(
    string Path,
    bool Json,
    int Take,
    string? Alias,
    string? Selector,
    string? IndexPath,
    string? MachinePath);

internal static class ManagerCommandParsing
{
    public static ManagerCommandOptions? Parse(
        List<string> arguments,
        Action help,
        bool allowAlias = false,
        bool allowIndexPath = false,
        bool allowSelector = false)
    {
        var path = Directory.GetCurrentDirectory();
        string? alias = null;
        string? selector = null;
        string? indexPath = null;
        string? machinePath = null;
        var take = 5;
        var json = false;

        for (var i = 0; i < arguments.Count; i++)
        {
            var arg = arguments[i];
            if (ArgParsing.IsHelp(arg))
            {
                help();
                return null;
            }

            switch (arg)
            {
                case "--json":
                    json = true;
                    break;
                case "--path":
                    if (!ArgParsing.TryTakeValue(arguments, ref i, "--path", out path)) return null;
                    break;
                case "--take":
                    if (!ArgParsing.TryTakeValue(arguments, ref i, "--take", out var takeText) ||
                        !int.TryParse(takeText, out take) || take < 0) return null;
                    break;
                case "--alias" when allowAlias:
                    if (!ArgParsing.TryTakeValue(arguments, ref i, "--alias", out alias)) return null;
                    break;
                case "--index-path" when allowIndexPath:
                    if (!ArgParsing.TryTakeValue(arguments, ref i, "--index-path", out indexPath)) return null;
                    break;
                case "--machine-path":
                    if (!ArgParsing.TryTakeValue(arguments, ref i, "--machine-path", out machinePath)) return null;
                    break;
                case "--selector" when allowSelector:
                    if (!ArgParsing.TryTakeValue(arguments, ref i, "--selector", out selector)) return null;
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        Console.Error.WriteLine($"Unknown option '{arg}'.");
                        return null;
                    }

                    if (allowSelector && selector is null)
                    {
                        selector = arg;
                    }
                    else
                    {
                        path = arg;
                    }
                    break;
            }
        }

        return new ManagerCommandOptions(path!, json, take, alias, selector, indexPath, machinePath);
    }
}
