using AURA.Domain.ProjectControl;

namespace AURA.Application.ProjectControl;

/// <summary>
/// Explicit mapping between the wire (JSON) string codes used by the project-control
/// contract and the Domain enums. Deliberately not based on Enum.ToString()/TryParse,
/// because several codes (AI_ANALYST, IN_PROGRESS) don't match the C# member names.
/// </summary>
public static class ProjectControlCodes
{
    public static string ToCode(ProjectStatus value) => value switch
    {
        ProjectStatus.Draft => "DRAFT",
        ProjectStatus.Active => "ACTIVE",
        ProjectStatus.Paused => "PAUSED",
        ProjectStatus.Blocked => "BLOCKED",
        ProjectStatus.Completed => "COMPLETED",
        ProjectStatus.Archived => "ARCHIVED",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProjectStatus."),
    };

    public static string ToCode(ProjectEventType value) => value switch
    {
        ProjectEventType.Claim => "CLAIM",
        ProjectEventType.Progress => "PROGRESS",
        ProjectEventType.Handoff => "HANDOFF",
        ProjectEventType.Blocker => "BLOCKER",
        ProjectEventType.Completion => "COMPLETION",
        ProjectEventType.Request => "REQUEST",
        ProjectEventType.Approval => "APPROVAL",
        ProjectEventType.Rework => "REWORK",
        ProjectEventType.Deployment => "DEPLOYMENT",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProjectEventType."),
    };

    public static string ToCode(ProjectAgent value) => value switch
    {
        ProjectAgent.Unassigned => "UNASSIGNED",
        ProjectAgent.Codex => "CODEX",
        ProjectAgent.ClaudeCode => "CLAUDE_CODE",
        ProjectAgent.Grok => "GROK",
        ProjectAgent.Devin => "DEVIN",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProjectAgent."),
    };

    public static string ToCode(ProjectActorType value) => value switch
    {
        ProjectActorType.Unassigned => "UNASSIGNED",
        ProjectActorType.Human => "HUMAN",
        ProjectActorType.AiAnalyst => "AI_ANALYST",
        ProjectActorType.Coder => "CODER",
        ProjectActorType.Ingestion => "INGESTION",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProjectActorType."),
    };

    public static string ToCode(ProjectWorkStatus value) => value switch
    {
        ProjectWorkStatus.InProgress => "IN_PROGRESS",
        ProjectWorkStatus.Blocked => "BLOCKED",
        ProjectWorkStatus.Completed => "COMPLETED",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProjectWorkStatus."),
    };

    public static bool TryParseEventType(string? value, out ProjectEventType result)
    {
        switch (Normalize(value))
        {
            case "CLAIM": result = ProjectEventType.Claim; return true;
            case "PROGRESS": result = ProjectEventType.Progress; return true;
            case "HANDOFF": result = ProjectEventType.Handoff; return true;
            case "BLOCKER": result = ProjectEventType.Blocker; return true;
            case "COMPLETION": result = ProjectEventType.Completion; return true;
            case "REQUEST": result = ProjectEventType.Request; return true;
            case "APPROVAL": result = ProjectEventType.Approval; return true;
            case "REWORK": result = ProjectEventType.Rework; return true;
            case "DEPLOYMENT": result = ProjectEventType.Deployment; return true;
            default: result = default; return false;
        }
    }

    public static bool TryParseActorType(string? value, out ProjectActorType result)
    {
        switch (Normalize(value))
        {
            case "UNASSIGNED": result = ProjectActorType.Unassigned; return true;
            case "HUMAN": result = ProjectActorType.Human; return true;
            case "AI_ANALYST": result = ProjectActorType.AiAnalyst; return true;
            case "CODER": result = ProjectActorType.Coder; return true;
            case "INGESTION": result = ProjectActorType.Ingestion; return true;
            default: result = default; return false;
        }
    }

    public static bool TryParseWorkStatus(string? value, out ProjectWorkStatus result)
    {
        switch (Normalize(value))
        {
            case "IN_PROGRESS": result = ProjectWorkStatus.InProgress; return true;
            case "BLOCKED": result = ProjectWorkStatus.Blocked; return true;
            case "COMPLETED": result = ProjectWorkStatus.Completed; return true;
            default: result = default; return false;
        }
    }

    public static bool TryParseProjectStatus(string? value, out ProjectStatus result)
    {
        switch (Normalize(value))
        {
            case "DRAFT": result = ProjectStatus.Draft; return true;
            case "ACTIVE": result = ProjectStatus.Active; return true;
            case "PAUSED": result = ProjectStatus.Paused; return true;
            case "BLOCKED": result = ProjectStatus.Blocked; return true;
            case "COMPLETED": result = ProjectStatus.Completed; return true;
            case "ARCHIVED": result = ProjectStatus.Archived; return true;
            default: result = default; return false;
        }
    }

    private static string? Normalize(string? value) => value?.Trim().ToUpperInvariant();
}
