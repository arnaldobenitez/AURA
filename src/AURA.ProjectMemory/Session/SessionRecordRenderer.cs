using System.Text;
using System.Text.RegularExpressions;
using AURA.ProjectMemory.Templates;

namespace AURA.ProjectMemory.Session;

/// <summary>
/// Renders a closed session as Markdown, sourcing its structure - metadata field order and
/// section headings, in order - directly from the normative embedded
/// docs/aura/project-memory/templates/SESSION.md. This is the only place that template's shape
/// is interpreted; it is never re-typed as a second, hand-maintained layout.
/// </summary>
public static partial class SessionRecordRenderer
{
    public static string Render(SessionState state, SessionCloseRequest closeRequest, string finishedAt)
    {
        var template = TemplateProvider.GetSessionTemplate();
        var fieldOrder = ExtractMetadataFieldOrder(template);
        var sectionOrder = ExtractSectionHeadings(template);

        var fieldValues = BuildFieldValues(state, finishedAt, closeRequest);
        var sectionValues = BuildSectionValues(state, closeRequest);

        var builder = new StringBuilder();
        builder.AppendLine($"# Sesión {state.SessionId}");
        builder.AppendLine();
        builder.AppendLine("## Metadatos");
        builder.AppendLine();
        builder.AppendLine("| Campo | Valor |");
        builder.AppendLine("|---|---|");

        foreach (var field in fieldOrder)
        {
            if (fieldValues.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                builder.AppendLine($"| `{field}` | `{value}` |");
            }
        }

        foreach (var section in sectionOrder)
        {
            builder.AppendLine();
            builder.AppendLine($"## {section}");
            builder.AppendLine();
            if (sectionValues.TryGetValue(section, out var content) && !string.IsNullOrWhiteSpace(content))
            {
                builder.AppendLine(content.Trim());
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, string?> BuildFieldValues(
        SessionState state, string finishedAt, SessionCloseRequest closeRequest) => new()
    {
        ["sessionId"] = state.SessionId,
        ["projectId"] = state.ProjectId,
        ["startedAt"] = state.StartedAt,
        ["finishedAt"] = finishedAt,
        ["machineId"] = state.MachineId,
        ["machineName"] = state.MachineName,
        ["logicalUser"] = state.LogicalUser,
        ["agent"] = state.Agent,
        ["model"] = state.Model,
        ["auraRole"] = state.AuraRole,
        ["osUser"] = closeRequest.OsUser,
        ["localProjectPath"] = closeRequest.LocalProjectPath,
        ["gitBranch"] = closeRequest.GitBranch,
        ["initialCommit"] = closeRequest.InitialCommit,
        ["finalCommit"] = closeRequest.FinalCommit,
        ["workingTreeInitial"] = closeRequest.WorkingTreeInitial,
        ["workingTreeFinal"] = closeRequest.WorkingTreeFinal,
        ["updatedDocuments"] = closeRequest.UpdatedDocuments is { Count: > 0 }
            ? string.Join(", ", closeRequest.UpdatedDocuments)
            : null,
    };

    private static Dictionary<string, string?> BuildSectionValues(
        SessionState state, SessionCloseRequest closeRequest)
    {
        var modifiedFiles = closeRequest.ModifiedFiles is { Count: > 0 }
            ? string.Join('\n', closeRequest.ModifiedFiles.Select(f => $"- {f}"))
            : null;

        return new Dictionary<string, string?>
        {
            ["Objetivo"] = state.Objective,
            ["Trabajo realizado"] = closeRequest.WorkPerformed,
            ["Resultado"] = closeRequest.Result,
            ["Archivos afectados"] = modifiedFiles,
            ["Decisiones"] = closeRequest.Decisions,
            ["Pruebas y validaciones"] = closeRequest.Tests,
            ["Problemas y bloqueos"] = closeRequest.Problems,
            ["Deuda detectada"] = closeRequest.TechnicalDebt,
            ["Pendientes"] = closeRequest.Pending,
            ["Próximo paso recomendado"] = closeRequest.NextRecommendedStep,
        };
    }

    private static List<string> ExtractMetadataFieldOrder(string template)
    {
        var fields = new List<string>();
        foreach (Match match in MetadataFieldRegex().Matches(template))
        {
            fields.Add(match.Groups[1].Value);
        }

        return fields;
    }

    private static List<string> ExtractSectionHeadings(string template)
    {
        var headings = new List<string>();
        foreach (Match match in SectionHeadingRegex().Matches(template))
        {
            var title = match.Groups[1].Value.Trim();
            if (!string.Equals(title, "Metadatos", StringComparison.Ordinal))
            {
                headings.Add(title);
            }
        }

        return headings;
    }

    [GeneratedRegex(@"^\|\s*`([a-zA-Z0-9]+)`\s*\|", RegexOptions.Multiline)]
    private static partial Regex MetadataFieldRegex();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex SectionHeadingRegex();
}
