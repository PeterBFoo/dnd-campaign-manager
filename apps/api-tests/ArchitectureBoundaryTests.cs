using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class ArchitectureBoundaryTests
{
    // Temporary safeguard: Application and Domain live in the same assembly as
    // Infrastructure, so this is a source scan rather than a compiler boundary.
    // It can miss global:: aliases, fully-qualified names without a using, generated
    // code, and comments/strings. It is not a substitute for separate projects.

    [Fact]
    public void Application_sources_do_not_reference_infrastructure_or_ef()
    {
        AssertNoForbiddenReferences(
            LocateLayer("Application"),
            ["Infrastructure", "Microsoft.EntityFrameworkCore", "Npgsql", "InvitationRecord"]);
    }

    [Fact]
    public void Domain_sources_do_not_reference_outer_layers_or_ef()
    {
        AssertNoForbiddenReferences(
            LocateLayer("Domain"),
            ["Infrastructure", "Application", "DndCampaign.Api.Api", "Microsoft.EntityFrameworkCore", "Npgsql", "InvitationRecord"]);
    }

    private static void AssertNoForbiddenReferences(string directory, IReadOnlyList<string> forbidden)
    {
        var violations = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(candidate => IsCodeLine(candidate.line)
                && forbidden.Any(token => candidate.line.Contains(token, StringComparison.Ordinal)))
            .Select(candidate => $"{Relative(candidate.path)}:{candidate.index + 1}: {candidate.line.Trim()}")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static bool IsCodeLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Length > 0
            && !trimmed.StartsWith("//", StringComparison.Ordinal)
            && !trimmed.StartsWith("///", StringComparison.Ordinal);
    }

    private static string LocateLayer(string layerName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "apps", "api", layerName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate apps/api/{layerName}.");
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
}
