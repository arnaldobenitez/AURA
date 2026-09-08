using System.Reflection;

namespace AURA.ProjectMemory.Templates;

/// <summary>
/// Resolves the normative Project Memory Core templates from embedded resources, so the tool
/// never depends on the AURA repository's on-disk path at runtime. The single source of truth
/// for template content remains docs/aura/project-memory/templates/*.md; the .csproj links those
/// exact files as embedded resources at build time (see AURA.ProjectMemory.csproj), so there is
/// still only one file to edit, never a second hand-maintained copy.
/// </summary>
public static class TemplateProvider
{
    private static readonly Assembly ResourceAssembly = typeof(TemplateProvider).Assembly;

    public static string GetAuraMd() => ReadTemplate("AURA.md");

    public static string GetProyectoTemplate() => ReadTemplate("00-PROYECTO.md");

    public static string GetEstadoActualTemplate() => ReadTemplate("01-ESTADO-ACTUAL.md");

    public static string GetSessionTemplate() => ReadTemplate("SESSION.md");

    private static string ReadTemplate(string fileName)
    {
        var resourceName = ResourceAssembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("." + fileName, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"AURA Project Memory template '{fileName}' is missing from the embedded resources. " +
                "This indicates a packaging defect in AURA.ProjectMemory, not a user error.");
        }

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open embedded resource stream for '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
