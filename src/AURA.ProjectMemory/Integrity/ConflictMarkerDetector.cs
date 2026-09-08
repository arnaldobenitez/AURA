namespace AURA.ProjectMemory.Integrity;

/// <summary>
/// Detects classic Git merge conflict markers (AURA_PROJECT_MEMORY v1.0 Etapa 6 §21/§22). Never
/// attempts to resolve a conflict - only flags one so it is never treated as trustworthy content.
/// </summary>
public static class ConflictMarkerDetector
{
    private const string StartMarker = "<<<<<<<";
    private const string MiddleMarker = "=======";
    private const string EndMarker = ">>>>>>>";

    public static bool ContainsConflictMarkers(string content)
    {
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith(StartMarker, StringComparison.Ordinal) ||
                line.StartsWith(EndMarker, StringComparison.Ordinal) ||
                line == MiddleMarker)
            {
                return true;
            }
        }

        return false;
    }
}
