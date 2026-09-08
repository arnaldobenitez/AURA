namespace AURA.ProjectMemory.Diagnostics;

/// <summary>
/// Stable, machine-readable diagnostic codes for AURA Project Memory tooling.
/// Codes are additive and must not be renumbered once shipped.
/// </summary>
public static class DiagnosticCode
{
    public const string MissingProjectManifest = "APM001";
    public const string InvalidProjectManifest = "APM002";
    public const string MissingCoreDocument = "APM003";
    public const string AbsoluteDocumentationPath = "APM004";
    public const string MissingOptionalModule = "APM005";
    public const string AgentsFileAbsent = "APM006";
    public const string MachineProfileAbsent = "APM007";
    public const string MachineProfileInvalid = "APM008";
    public const string HistoryNotDirectory = "APM009";
    public const string AlreadyInitialized = "APM010";
    public const string ProjectRootNotFound = "APM011";
    public const string SchemaVersionUnsupported = "APM012";
    public const string DocumentationTargetMissing = "APM013";
    public const string PartialInitialization = "APM014";

    // Session lifecycle (Etapa 5) - never renumber; add new codes instead.
    public const string SessionNotFound = "APM101";
    public const string SessionAlreadyClosed = "APM102";
    public const string SessionProjectMismatch = "APM103";
    public const string SessionHistoryCollision = "APM104";
    public const string InvalidSessionPayload = "APM105";
    public const string MachineProfileMissingForSession = "APM106";
    public const string HistoryWriteFailed = "APM107";

    // Git and integrity (Etapa 6) - never renumber; add new codes instead.
    public const string GitUnavailable = "APM201";
    public const string NotGitRepository = "APM202";
    public const string GitCommandFailed = "APM203";
    public const string GitMergeConflict = "APM204";
    public const string CurrentStateConflictMarkers = "APM205";
    public const string HistoryConflictMarkers = "APM206";
    public const string GitMetadataMismatch = "APM207";

    // Agent contract (Etapa 7) - never renumber; add new codes instead.
    public const string ProjectMemoryInvalid = "APM301";
}
