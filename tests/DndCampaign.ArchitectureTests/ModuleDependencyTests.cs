using System.Reflection;
using System.Xml.Linq;
using DndCampaign.Modules.Access;
using Xunit;

namespace DndCampaign.ArchitectureTests;

public sealed class ModuleDependencyTests
{
    private static readonly Assembly Access = typeof(AccessModule).Assembly;
    private static readonly Assembly Host = typeof(Program).Assembly;

    [Fact]
    public void Each_module_is_exactly_one_project()
    {
        var modulesDirectory = Path.Combine(FindRepositoryRoot(), "src", "Modules");
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
    public void Module_projects_do_not_reference_other_projects()
    {
        var modulesDirectory = Path.Combine(FindRepositoryRoot(), "src", "Modules");
        var violations = Directory.EnumerateFiles(modulesDirectory, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(project => XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => $"{Path.GetRelativePath(modulesDirectory, project)} -> {reference}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Modules_do_not_reference_the_host() => Assert.DoesNotContain(
        Access.GetReferencedAssemblies(),
        reference => reference.Name == Host.GetName().Name);

    [Fact]
    public void Modules_do_not_reference_other_module_implementations()
    {
        var modules = new[] { Access };
        var moduleNames = modules.Select(module => module.GetName().Name!).ToHashSet(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            var unexpected = module.GetReferencedAssemblies().FirstOrDefault(reference =>
                reference.Name is not null
                && reference.Name.StartsWith("DndCampaign.Modules.", StringComparison.Ordinal)
                && moduleNames.Contains(reference.Name)
                && reference.Name != module.GetName().Name);
            Assert.Null(unexpected);
        }
    }

    [Fact]
    public void Host_only_uses_module_facades()
    {
        var hostDirectory = Path.Combine(FindRepositoryRoot(), "apps", "api");
        var forbidden = new[]
        {
            "DndCampaign.Modules.Access.Api",
            "DndCampaign.Modules.Access.Application",
            "DndCampaign.Modules.Access.Domain",
            "DndCampaign.Modules.Access.Infrastructure",
            "AccessDbContext",
        };
        var violations = Directory.EnumerateFiles(hostDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => forbidden
                .Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(hostDirectory, file)} -> {token}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Module_graph_is_acyclic()
    {
        var modules = new[] { Access };
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
}
