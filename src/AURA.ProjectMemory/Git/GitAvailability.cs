namespace AURA.ProjectMemory.Git;

/// <summary>
/// Result of probing for Git without treating its absence as an error (AURA_PROJECT_MEMORY v1.0
/// keeps Git strictly optional - see Etapa 6 §7/§15/§16).
/// </summary>
public enum GitAvailability
{
    /// <summary>No usable <c>git</c> executable was found.</summary>
    Unavailable,

    /// <summary>Git is available, but the path is not inside a work tree.</summary>
    NotRepository,

    /// <summary>Git is available and the path is inside a repository.</summary>
    Available
}
