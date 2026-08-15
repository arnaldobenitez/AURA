namespace AURA.Api.Correlation;

public static class CorrelationIdResolver
{
    public const string HeaderName = "X-Correlation-Id";

    public static string Resolve(string? incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming) && Guid.TryParse(incoming, out var parsed) && parsed != Guid.Empty)
        {
            return parsed.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
