using System.Text;
using System.Text.RegularExpressions;

namespace AURA.ProjectMemory.Session;

/// <summary>
/// Implements the history file naming convention from AURA_PROJECT_MEMORY v1.0 §11:
/// <c>YYYY-MM-DDTHH-mm-ssZ__&lt;sessionId&gt;__&lt;slug&gt;.md</c>, UTC in the filename, slug optional.
/// Uniqueness comes from <c>sessionId</c>, never from the slug or the timestamp.
/// </summary>
public static partial class SessionFileNameGenerator
{
    private const int MaxSlugLength = 60;

    public static string Generate(DateTimeOffset finishedAt, string sessionId, string? rawSlug)
    {
        var utcStamp = finishedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ");
        var slug = Sanitize(rawSlug);

        return string.IsNullOrEmpty(slug)
            ? $"{utcStamp}__{sessionId}.md"
            : $"{utcStamp}__{sessionId}__{slug}.md";
    }

    public static string? Sanitize(string? rawSlug)
    {
        if (string.IsNullOrWhiteSpace(rawSlug))
        {
            return null;
        }

        var lowered = rawSlug.Trim().ToLowerInvariant();
        var replaced = DisallowedCharsRegex().Replace(lowered, "-");
        var collapsed = RepeatedDashRegex().Replace(replaced, "-").Trim('-');

        if (collapsed.Length == 0)
        {
            return null;
        }

        return collapsed.Length > MaxSlugLength
            ? collapsed[..MaxSlugLength].Trim('-')
            : collapsed;
    }

    [GeneratedRegex("[^a-z0-9-]+")]
    private static partial Regex DisallowedCharsRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedDashRegex();
}
