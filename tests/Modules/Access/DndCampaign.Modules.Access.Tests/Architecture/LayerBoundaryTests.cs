using DndCampaign.Modules.Access;
using Xunit;

namespace DndCampaign.Modules.Access.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade()
    {
        var exportedTypes = typeof(AccessModule).Assembly.GetExportedTypes();

        Assert.Equal([typeof(AccessModule)], exportedTypes);
    }

    [Fact]
    public void Api_uses_mvc_controllers_instead_of_minimal_api_route_mapping()
    {
        var apiDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Modules",
            "Access",
            "DndCampaign.Modules.Access",
            "Api");
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(apiDirectory, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.Contains("ControllerBase", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGroup(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGet(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapDelete(", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Domain", "DndCampaign.Modules.Access.Application", "DndCampaign.Modules.Access.Api", "DndCampaign.Modules.Access.Infrastructure", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "OpenTelemetry")]
    [InlineData("Application", "DndCampaign.Modules.Access.Api", "DndCampaign.Modules.Access.Infrastructure", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "OpenTelemetry", "Npgsql")]
    [InlineData("Api", "DndCampaign.Modules.Access.Infrastructure", "Microsoft.EntityFrameworkCore", "Npgsql", "DbContext", "DbSet", "SaveChanges")]
    [InlineData("Infrastructure", "DndCampaign.Modules.Access.Api", "", "", "", "", "")]
    public void Internal_layer_does_not_depend_on_forbidden_layers_or_frameworks(
        string layer,
        params string[] forbiddenTokens)
    {
        var layerDirectory = Path.Combine(FindRepositoryRoot(), "src", "Modules", "Access", "DndCampaign.Modules.Access", layer);
        var violations = Directory.EnumerateFiles(layerDirectory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => forbiddenTokens
                .Where(token => !string.IsNullOrEmpty(token) && File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(layerDirectory, file)} -> {token}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
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
