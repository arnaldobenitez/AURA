using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Paths;
using AURA.ProjectMemory.Resolution;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Agent;

/// <summary>
/// Builds the universal agent bootstrap context (AURA_AGENT_CONTRACT v1.0). It only reads; it
/// never modifies the project. It reuses <see cref="ProjectContextService"/> for low-level
/// discovery and adds the contract layer.
/// </summary>
public static class AgentBootstrapService
{
    private static readonly string[] CoreDocuments =
    [
        "AURA.md",
        ".aura/project.json",
        "docs/00-PROYECTO.md",
        "docs/01-ESTADO-ACTUAL.md"
    ];

    public static AgentBootstrapContext Bootstrap(
        string startPath,
        string? machineProfilePath = null,
        string? runtimeRoot = null)
    {
        var runtimeContext = ProjectContextService.Get(startPath, machineProfilePath, runtimeRoot);
        var project = runtimeContext.Project;

        var projectMemoryInfo = project.IsValid && project.ProjectRoot is not null
            ? BuildProjectMemoryInfo(project.ProjectRoot)
            : new AgentProjectMemoryInfo("unknown", CoreDocuments.ToList(), []);

        var machine = runtimeContext.Machine is null
            ? null
            : new AgentMachineInfo(
                runtimeContext.Machine.IsValid,
                runtimeContext.Machine.MachineId,
                runtimeContext.Machine.MachineName,
                runtimeContext.Machine.ProfilePath);

        var git = new AgentGitInfo(
            runtimeContext.Git.Availability != GitAvailability.Unavailable,
            runtimeContext.Git.Availability == GitAvailability.Available,
            runtimeContext.Git.Branch,
            runtimeContext.Git.Commit,
            runtimeContext.Git.WorkingTree.ToString().ToLowerInvariant(),
            runtimeContext.Git.HasMergeConflicts);

        var sessions = new AgentSessionInfo(runtimeContext.OpenSessions, runtimeContext.OpenSessions.Count > 0);

        var contract = new AgentContractPolicy(
            true,
            true,
            true,
            [
                "cambio de código",
                "cambio de configuración",
                "cambio de arquitectura",
                "decisión funcional o técnica",
                "análisis sustancial",
                "nuevo bloqueo o resolución",
                "cambio relevante de backlog",
                "pruebas significativas",
                "deuda técnica relevante"
            ],
            [
                "consulta trivial",
                "pregunta simple",
                "lectura sin conclusión",
                "explicación breve sin impacto"
            ],
            true);

        var commands = new AgentLifecycleCommands(
            "aura agent bootstrap",
            "aura project session start --objective <text> --role <role>",
            "aura project session status",
            "aura project session close --session <uuid> --input <file>");

        var documentation = new AgentDocumentationPolicy(
        [
            new("estado operativo del proyecto", "docs/01-ESTADO-ACTUAL.md"),
            new("decisión importante", "docs/02-DECISIONES.md"),
            new("nuevo pendiente o cambio en backlog", "docs/03-BACKLOG.md"),
            new("cambio estructural", "docs/04-ARQUITECTURA.md"),
            new("resumen de sesión cerrada", "docs/historial/<timestamp>__<sessionId>.md")
        ]);

        return new AgentBootstrapContext(
            new AgentProjectInfo(
                project.IsValid,
                project.ProjectRoot,
                project.ProjectId,
                project.ProjectName,
                project.ValidationDiagnostics),
            projectMemoryInfo,
            machine,
            git,
            sessions,
            contract,
            commands,
            documentation,
            runtimeContext.Diagnostics);
    }

    private static AgentProjectMemoryInfo BuildProjectMemoryInfo(string projectRoot)
    {
        var version = "unknown";
        var modules = new List<AgentModuleInfo>();

        var resolution = ProjectRootResolver.Resolve(projectRoot);
        if (resolution.Manifest is not null)
        {
            version = resolution.Manifest.ProjectMemoryVersion;
            modules.AddRange(BuildModules(projectRoot, resolution.Manifest));
        }

        return new AgentProjectMemoryInfo(version, CoreDocuments.ToList(), modules);
    }

    private static IEnumerable<AgentModuleInfo> BuildModules(
        string projectRoot, ProjectManifest manifest)
    {
        foreach (var kvp in manifest.Documentation.Extra)
        {
            var present = RelativePathResolver.TryResolveSafePath(projectRoot, kvp.Value, out var absolutePath) &&
                          (File.Exists(absolutePath) || Directory.Exists(absolutePath));

            yield return new AgentModuleInfo(kvp.Key, kvp.Value, present);
        }

        if (!manifest.Documentation.Extra.ContainsKey("02-DECISIONES"))
            yield return new AgentModuleInfo("02-DECISIONES", "docs/02-DECISIONES.md", File.Exists(Path.Combine(projectRoot, "docs", "02-DECISIONES.md")));
        if (!manifest.Documentation.Extra.ContainsKey("03-BACKLOG"))
            yield return new AgentModuleInfo("03-BACKLOG", "docs/03-BACKLOG.md", File.Exists(Path.Combine(projectRoot, "docs", "03-BACKLOG.md")));
        if (!manifest.Documentation.Extra.ContainsKey("04-ARQUITECTURA"))
            yield return new AgentModuleInfo("04-ARQUITECTURA", "docs/04-ARQUITECTURA.md", File.Exists(Path.Combine(projectRoot, "docs", "04-ARQUITECTURA.md")));
    }
}
