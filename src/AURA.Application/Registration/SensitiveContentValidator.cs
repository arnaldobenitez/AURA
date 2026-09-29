using System.Text.RegularExpressions;

namespace AURA.Application.Registration;

/// <summary>
/// Detects common credential material before registration events are persisted. The patterns
/// are intentionally conservative: the registration channel must reject obvious secrets rather
/// than redact and accidentally retain enough material to reconstruct them.
/// </summary>
internal static class SensitiveContentValidator
{
    private static readonly Regex JwtToken = new(
        @"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex PrivateKey = new(
        @"-----BEGIN\s+[A-Z0-9 ]*PRIVATE KEY-----",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ConnectionString = new(
        @"\b(?:Server|Data\s+Source|Initial\s+Catalog|User\s+ID|Password|Pwd|Integrated\s+Security|Trusted_Connection)\s*=",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex NamedSecret = new(
        @"\b(?:password|passwd|pwd|secret|token|api[-_ ]?key|admin[-_ ]?api[-_ ]?key|signing[-_ ]?key|connection[-_ ]?string|arca[-_ ]?token|wsaa[-_ ]?token)\s*(?:=|:)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex BearerToken = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]{12,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ArcaXmlCredential = new(
        @"<\s*(?:token|sign)\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public static bool ContainsSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return JwtToken.IsMatch(value)
            || PrivateKey.IsMatch(value)
            || ConnectionString.IsMatch(value)
            || NamedSecret.IsMatch(value)
            || BearerToken.IsMatch(value)
            || ArcaXmlCredential.IsMatch(value);
    }
}
