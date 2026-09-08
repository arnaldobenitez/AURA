using System.Globalization;
using System.Text.RegularExpressions;
using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Manifest;
using AURA.ProjectMemory.Paths;
using AURA.ProjectMemory.Resolution;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Manager;

/// <summary>
/// Reusable, read-oriented Project Manager facade. It composes existing discovery, validation,
/// Git, machine and session services; it does not create sessions or alter project files.
/// </summary>
public static partial class ProjectManagerService
{
    private static readonly string[] DefaultOptionalModules = ["02-DECISIONES", "03-BACKLOG", "04-ARQUITECTURA"];

    public static ProjectManagerInspection Inspect(
        string startPath, int historyTake = 5, string? machineProfilePath = null, string? runtimeRoot = null)
    {
        var resolution = ProjectRootResolver.Resolve(startPath);
        var context = ProjectContextService.Get(startPath, machineProfilePath, runtimeRoot);
        var validation = ProjectMemoryValidator.Validate(startPath, machineProfilePath: machineProfilePath);
        var diagnostics = MergeDiagnostics(validation.Diagnostics, context.Diagnostics);

        if (!resolution.Found || resolution.Manifest is null || resolution.ProjectRoot is null)
        {
            return new ProjectManagerInspection(
                null, null, null, null, false, diagnostics, null, [], [],
                ToMachine(context.Machine), ToGit(context.Git), [], []);
        }

        var root = resolution.ProjectRoot;
        var manifest = resolution.Manifest;
        var corePaths = new[]
        {
            ("AURA.md", "AURA.md"),
            ("project", ".aura/project.json"),
            ("projectDocument", manifest.Documentation.Project),
            ("currentState", manifest.Documentation.CurrentState)
        };
        var optional = BuildOptionalModules(root, manifest);
        var history = ReadHistory(root, manifest.Documentation.History, historyTake);

        return new ProjectManagerInspection(
            root,
            manifest.ProjectId,
            manifest.ProjectName,
            manifest.ProjectMemoryVersion,
            validation.IsValid,
            diagnostics,
            manifest.Documentation.CurrentState,
            corePaths.GroupBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(pair => Document(root, pair.Item2, pair.Item1)).ToList(),
            optional,
            ToMachine(context.Machine),
            ToGit(context.Git),
            context.OpenSessions,
            history);
    }

    public static ManagerOperationResult<RememberedProject> Remember(
        string startPath, string? alias = null, string? indexPath = null)
    {
        var validation = ProjectMemoryValidator.Validate(startPath);
        var resolution = ProjectRootResolver.Resolve(startPath);
        if (!validation.IsValid || resolution.Manifest is null || resolution.ProjectRoot is null)
        {
            return new ManagerOperationResult<RememberedProject>(false, null, validation.Diagnostics);
        }

        var normalizedAlias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        var index = ProjectManagerIndexStore.Load(indexPath);
        var projects = index.Projects.ToList();
        var existing = projects.FirstOrDefault(p => p.ProjectId == resolution.Manifest.ProjectId);
        var otherAlias = normalizedAlias is null
            ? null
            : projects.FirstOrDefault(p => p.ProjectId != resolution.Manifest.ProjectId &&
                string.Equals(p.Alias, normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (otherAlias is not null)
        {
            return new ManagerOperationResult<RememberedProject>(false, null,
                [ProjectManagerIndexStore.AliasConflict(normalizedAlias!)]);
        }

        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var remembered = existing is null
            ? new RememberedProject(resolution.Manifest.ProjectId, normalizedAlias, resolution.Manifest.ProjectName,
                resolution.ProjectRoot, now, now)
            : existing with
            {
                Alias = normalizedAlias ?? existing.Alias,
                ProjectName = resolution.Manifest.ProjectName,
                ProjectRoot = resolution.ProjectRoot,
                LastSeenAt = now
            };

        if (existing is not null)
        {
            projects.Remove(existing);
        }

        projects.Add(remembered);
        ProjectManagerIndexStore.Save(new ProjectManagerIndex(projects), indexPath);
        return new ManagerOperationResult<RememberedProject>(true, remembered, []);
    }

    public static IReadOnlyList<KnownProject> ListKnown(string? indexPath = null)
    {
        return ProjectManagerIndexStore.Load(indexPath).Projects.Select(entry =>
        {
            var exists = Directory.Exists(entry.ProjectRoot);
            if (!exists)
            {
                return new KnownProject(entry, false, false, []);
            }

            var validation = ProjectMemoryValidator.Validate(entry.ProjectRoot);
            return new KnownProject(entry, true, validation.IsValid, validation.Diagnostics);
        }).ToList();
    }

    public static ManagerOperationResult<RememberedProject> Forget(string selector, string? indexPath = null)
    {
        var index = ProjectManagerIndexStore.Load(indexPath);
        var entry = index.Projects.FirstOrDefault(p =>
            string.Equals(p.ProjectId, selector, StringComparison.OrdinalIgnoreCase) ||
            (p.Alias is not null && string.Equals(p.Alias, selector, StringComparison.OrdinalIgnoreCase)));

        if (entry is null)
        {
            return new ManagerOperationResult<RememberedProject>(false, null,
                [ProjectManagerIndexStore.ProjectNotFound(selector)]);
        }

        ProjectManagerIndexStore.Save(new ProjectManagerIndex(index.Projects.Where(p => p != entry).ToList()), indexPath);
        return new ManagerOperationResult<RememberedProject>(true, entry, []);
    }

    public static IReadOnlyList<ManagerHistoryEntry> History(string startPath, int take = 5)
    {
        var resolution = ProjectRootResolver.Resolve(startPath);
        return resolution.Found && resolution.Manifest is not null && resolution.ProjectRoot is not null
            ? ReadHistory(resolution.ProjectRoot, resolution.Manifest.Documentation.History, take)
            : [];
    }

    public static ProjectManagerHandoff Handoff(
        string startPath, int historyTake = 5, string? machineProfilePath = null, string? runtimeRoot = null)
    {
        var inspection = Inspect(startPath, historyTake, machineProfilePath, runtimeRoot);
        var summary = inspection.CurrentStatePath is null || inspection.ProjectRoot is null
            ? null
            : ReadContinuitySummary(inspection.ProjectRoot, inspection.CurrentStatePath);

        return new ProjectManagerHandoff(
            inspection.ProjectRoot,
            inspection.ProjectId,
            inspection.ProjectName,
            inspection.Valid,
            inspection.CurrentStatePath,
            summary,
            inspection.OpenSessions,
            inspection.LastHistoryEntries,
            inspection.Git,
            ExtractNextSteps(summary),
            inspection.Diagnostics,
            "aura agent bootstrap --path <PROJECT_ROOT> --json");
    }

    private static IReadOnlyList<ManagerDocument> BuildOptionalModules(string root, ProjectManifest manifest)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in manifest.Documentation.Extra)
        {
            paths[pair.Key] = pair.Value;
        }

        foreach (var module in DefaultOptionalModules)
        {
            paths.TryAdd(module, $"docs/{module}.md");
        }

        return paths.Select(pair => Document(root, pair.Value, pair.Key)).ToList();
    }

    private static ManagerDocument Document(string root, string path, string? displayPath = null)
    {
        return new ManagerDocument(displayPath ?? path, path,
            RelativePathResolver.TryResolveSafePath(root, path, out var absolute) && File.Exists(absolute));
    }

    private static ManagerMachine ToMachine(MachineValidationResult? machine) => machine is null
        ? new ManagerMachine(false, false, null, null, null)
        : new ManagerMachine(true, machine.IsValid, machine.MachineId, machine.MachineName, machine.ProfilePath);

    private static ManagerGit ToGit(GitContext git) => new(
        git.Availability != GitAvailability.Unavailable,
        git.Availability == GitAvailability.Available,
        git.Branch,
        git.Commit,
        git.WorkingTree.ToString().ToLowerInvariant(),
        git.HasMergeConflicts);

    private static IReadOnlyList<Diagnostic> MergeDiagnostics(
        IEnumerable<Diagnostic> first, IEnumerable<Diagnostic> second)
    {
        return first.Concat(second)
            .GroupBy(d => $"{d.Code}|{d.Path}|{d.Message}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<ManagerHistoryEntry> ReadHistory(string root, string historyPath, int take)
    {
        if (take <= 0 || !RelativePathResolver.TryResolveSafePath(root, historyPath, out var directory) ||
            !Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, "*.md")
            .Select(path => ParseHistory(root, path))
            .OrderByDescending(entry => entry.Timestamp ?? string.Empty, StringComparer.Ordinal)
            .ThenByDescending(entry => entry.FileName, StringComparer.Ordinal)
            .Take(take)
            .ToList();
    }

    private static ManagerHistoryEntry ParseHistory(string root, string path)
    {
        var fileName = Path.GetFileName(path);
        var match = HistoryFileRegex().Match(fileName);
        string? title = null;
        string? summary = null;
        try
        {
            var lines = File.ReadAllLines(path);
            title = lines.FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim();
            summary = SectionValue(lines, "Resultado");
        }
        catch (IOException)
        {
            // The metadata remains useful even when a concurrent read cannot load the content.
        }

        return new ManagerHistoryEntry(
            fileName,
            match.Success ? match.Groups[2].Value : null,
            match.Success ? match.Groups[1].Value : null,
            match.Success && match.Groups[3].Success ? match.Groups[3].Value : null,
            ToPortablePath(root, path),
            title,
            summary);
    }

    private static string? SectionValue(string[] lines, string section)
    {
        var heading = $"## {section}";
        var index = Array.FindIndex(lines, line => string.Equals(line.Trim(), heading, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;

        var content = lines.Skip(index + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)).ToArray();
        var value = string.Join(" ", content.Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("<!--", StringComparison.Ordinal) &&
                           !line.EndsWith("-->", StringComparison.Ordinal)));
        return value.Length == 0 ? null : value;
    }

    private static string? ReadContinuitySummary(string root, string statePath)
    {
        if (!RelativePathResolver.TryResolveSafePath(root, statePath, out var fullPath) || !File.Exists(fullPath))
            return null;

        try
        {
            var lines = File.ReadAllLines(fullPath);
            var sections = new[] { "Resumen ejecutivo", "Pendiente inmediato", "Bloqueos", "Próximo paso recomendado" };
            var values = sections.Select(section => (section, Value: SectionValue(lines, section)))
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => $"{item.section}: {item.Value}")
                .ToList();
            return values.Count == 0 ? null : string.Join(Environment.NewLine, values);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ExtractNextSteps(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return [];
        return summary.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("Próximo paso recomendado:", StringComparison.OrdinalIgnoreCase) ||
                           line.StartsWith("Pendiente inmediato:", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string ToPortablePath(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}T[^_]+Z)__([0-9a-fA-F-]{36})(?:__([a-z0-9-]+))?\.md$")]
    private static partial Regex HistoryFileRegex();
}
