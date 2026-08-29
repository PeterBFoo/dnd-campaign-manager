using System.Reflection;
using System.Xml.Linq;
using DndCampaign.Modules.Access;
using DndCampaign.Modules.AdventureCatalog;
using DndCampaign.Modules.Campaigns;
using DndCampaign.Modules.Characters;
using DndCampaign.Modules.Combat;
using DndCampaign.Modules.Journal;
using DndCampaign.Modules.Missions;
using Xunit;

namespace DndCampaign.ArchitectureTests;

public sealed class ModuleDependencyTests
{
    private static readonly Assembly Access = typeof(AccessModule).Assembly;
    private static readonly Assembly AdventureCatalog = typeof(AdventureCatalogModule).Assembly;
    private static readonly Assembly Campaigns = typeof(CampaignsModule).Assembly;
    private static readonly Assembly Characters = typeof(CharactersModule).Assembly;
    private static readonly Assembly Combat = typeof(CombatModule).Assembly;
    private static readonly Assembly Journal = typeof(JournalModule).Assembly;
    private static readonly Assembly Missions = typeof(MissionsModule).Assembly;
    private static readonly Assembly Host = typeof(Program).Assembly;

    [Fact]
    public void Backend_modules_live_inside_the_api_application()
    {
        Assert.True(Directory.Exists(GetModulesDirectory()));
        var legacyModulesDirectory = Path.Combine(FindRepositoryRoot(), "src", "Modules");
        Assert.False(
            Directory.Exists(legacyModulesDirectory)
            && Directory.EnumerateFiles(legacyModulesDirectory, "*.csproj", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public void Each_module_is_exactly_one_project()
    {
        var modulesDirectory = GetModulesDirectory();
        var violations = Directory.EnumerateDirectories(modulesDirectory)
            .Select(module => new
            {
                Name = Path.GetFileName(module),
                Projects = Directory.EnumerateFiles(module, "*.csproj", SearchOption.AllDirectories).ToArray(),
            })
            .Where(module => module.Projects.Length != 1)
            .Select(module => $"{module.Name}: {module.Projects.Length} projects")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Module_project_references_follow_the_approved_direction()
    {
        var modulesDirectory = GetModulesDirectory();
        var violations = Directory.EnumerateFiles(modulesDirectory, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(project => XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => new
                {
                    Project = Path.GetRelativePath(modulesDirectory, project),
                    Reference = reference!,
                }))
            .Where(edge => !IsApprovedEdge(edge.Project, edge.Reference))
            .Select(edge => $"{edge.Project} -> {edge.Reference}")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Modules_do_not_reference_the_host() => Assert.DoesNotContain(
        new[] { Access, AdventureCatalog, Campaigns, Characters, Combat, Journal, Missions }.SelectMany(module => module.GetReferencedAssemblies()),
        reference => reference.Name == Host.GetName().Name);

    [Fact]
    public void Modules_do_not_reference_other_module_implementations()
    {
        var modules = new[] { Access, AdventureCatalog, Campaigns, Characters, Combat, Journal, Missions };
        var moduleNames = modules.Select(module => module.GetName().Name!).ToHashSet(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            var dependencies = module.GetReferencedAssemblies().Where(reference =>
                reference.Name is not null
                && reference.Name.StartsWith("DndCampaign.Modules.", StringComparison.Ordinal)
                && moduleNames.Contains(reference.Name)
                && reference.Name != module.GetName().Name)
                .Select(reference => reference.Name!)
                .ToHashSet(StringComparer.Ordinal);
            if (module == Campaigns)
            {
                Assert.Subset(new HashSet<string>([Access.GetName().Name!, AdventureCatalog.GetName().Name!]), dependencies);
            }
            else if (module == Characters)
            {
                Assert.Subset(new HashSet<string>([Access.GetName().Name!, Campaigns.GetName().Name!]), dependencies);
            }
            else if (module == Journal)
            {
                Assert.Subset(new HashSet<string>([Campaigns.GetName().Name!, Characters.GetName().Name!]), dependencies);
            }
            else if (module == Combat)
            {
                Assert.Subset(new HashSet<string>([Campaigns.GetName().Name!, Characters.GetName().Name!]), dependencies);
            }
            else if (module == Missions)
            {
                Assert.Subset(new HashSet<string>([Campaigns.GetName().Name!, Characters.GetName().Name!]), dependencies);
            }
            else
            {
                Assert.Empty(dependencies);
            }
        }
    }

    [Fact]
    public void Host_only_uses_module_facades()
    {
        var hostDirectory = Path.Combine(FindRepositoryRoot(), "apps", "api");
        var modulesDirectory = Path.Combine(hostDirectory, "Modules") + Path.DirectorySeparatorChar;
        var forbidden = new[]
        {
            "DndCampaign.Modules.Access.Api",
            "DndCampaign.Modules.Access.Application",
            "DndCampaign.Modules.Access.Domain",
            "DndCampaign.Modules.Access.Infrastructure",
            "AccessDbContext",
            "DndCampaign.Modules.AdventureCatalog.Api",
            "DndCampaign.Modules.AdventureCatalog.Application",
            "DndCampaign.Modules.AdventureCatalog.Domain",
            "DndCampaign.Modules.AdventureCatalog.Infrastructure",
            "AdventureCatalogDbContext",
            "DndCampaign.Modules.Campaigns.Api",
            "DndCampaign.Modules.Campaigns.Application",
            "DndCampaign.Modules.Campaigns.Domain",
            "DndCampaign.Modules.Campaigns.Infrastructure",
            "CampaignsDbContext",
            "DndCampaign.Modules.Characters.Api",
            "DndCampaign.Modules.Characters.Application",
            "DndCampaign.Modules.Characters.Domain",
            "DndCampaign.Modules.Characters.Infrastructure",
            "CharactersDbContext",
            "DndCampaign.Modules.Combat.Api",
            "DndCampaign.Modules.Combat.Application",
            "DndCampaign.Modules.Combat.Domain",
            "DndCampaign.Modules.Combat.Infrastructure",
            "CombatDbContext",
            "DndCampaign.Modules.Journal.Api",
            "DndCampaign.Modules.Journal.Application",
            "DndCampaign.Modules.Journal.Domain",
            "DndCampaign.Modules.Journal.Infrastructure",
            "JournalDbContext",
            "DndCampaign.Modules.Missions.Api",
            "DndCampaign.Modules.Missions.Application",
            "DndCampaign.Modules.Missions.Domain",
            "DndCampaign.Modules.Missions.Infrastructure",
            "MissionsDbContext",
            "DndCampaign.Modules.AdventureCatalog.Api",
            "DndCampaign.Modules.AdventureCatalog.Application",
            "DndCampaign.Modules.AdventureCatalog.Domain",
            "DndCampaign.Modules.AdventureCatalog.Infrastructure",
            "AdventureCatalogDbContext",
        };
        var violations = Directory.EnumerateFiles(hostDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.StartsWith(modulesDirectory, StringComparison.Ordinal))
            .SelectMany(file => forbidden
                .Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(hostDirectory, file)} -> {token}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Host_does_not_compile_module_sources() => Assert.DoesNotContain(
        Host.DefinedTypes,
        type => type.Namespace?.StartsWith("DndCampaign.Modules.", StringComparison.Ordinal) == true);

    [Fact]
    public void Module_graph_is_acyclic()
    {
        var modules = new[] { Access, AdventureCatalog, Campaigns, Characters, Combat, Journal, Missions };
        var names = modules.ToDictionary(module => module.GetName().Name!, StringComparer.Ordinal);
        var edges = modules.ToDictionary(
            module => module.GetName().Name!,
            module => module.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name is not null && names.ContainsKey(name))
                .Select(name => name!)
                .ToArray(),
            StringComparer.Ordinal);

        foreach (var module in modules)
        {
            Assert.False(HasCycle(module.GetName().Name!, edges, [], []));
        }
    }

    private static bool HasCycle(
        string current,
        IReadOnlyDictionary<string, string[]> edges,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (!visiting.Add(current))
        {
            return true;
        }

        if (!visited.Add(current))
        {
            visiting.Remove(current);
            return false;
        }

        foreach (var dependency in edges[current])
        {
            if (HasCycle(dependency, edges, visiting, visited))
            {
                return true;
            }
        }

        visiting.Remove(current);
        return false;
    }

    private static bool IsApprovedEdge(string project, string reference) =>
        (project.StartsWith($"Campaigns{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && (reference.Contains($"{Path.DirectorySeparatorChar}Access{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || reference.Contains($"{Path.DirectorySeparatorChar}AdventureCatalog{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        || (project.StartsWith($"Characters{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && (reference.Contains($"{Path.DirectorySeparatorChar}Campaigns{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || reference.Contains($"{Path.DirectorySeparatorChar}Access{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        || (project.StartsWith($"Journal{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && (reference.Contains($"{Path.DirectorySeparatorChar}Campaigns{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || reference.Contains($"{Path.DirectorySeparatorChar}Characters{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        || (project.StartsWith($"Combat{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && (reference.Contains($"{Path.DirectorySeparatorChar}Campaigns{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || reference.Contains($"{Path.DirectorySeparatorChar}Characters{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        || (project.StartsWith($"Missions{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && (reference.Contains($"{Path.DirectorySeparatorChar}Campaigns{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || reference.Contains($"{Path.DirectorySeparatorChar}Characters{Path.DirectorySeparatorChar}", StringComparison.Ordinal)));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "DndCampaign.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string GetModulesDirectory() =>
        Path.Combine(FindRepositoryRoot(), "apps", "api", "Modules");
}
