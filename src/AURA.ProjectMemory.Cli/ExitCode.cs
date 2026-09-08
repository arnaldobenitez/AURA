namespace AURA.ProjectMemory.Cli;

/// <summary>Process exit codes used consistently by every subcommand.</summary>
internal static class ExitCode
{
    public const int Success = 0;
    public const int OperationFailed = 1;
    public const int InvalidArguments = 2;
}
