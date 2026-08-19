using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class PlatformHealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public PlatformHealthTests(WebApplicationFactory<Program> factory)
    {
        client = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("OTEL_SDK_DISABLED", "true");
                builder.UseSetting(
                    "ConnectionStrings:Campaigns",
                    "Host=localhost;Port=5432;Database=test;Username=test;Password=test-only");
            })
            .CreateClient();
    }

    [Fact]
    public async Task Liveness_is_healthy_without_external_dependencies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/health/live", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("healthy", payload, StringComparison.OrdinalIgnoreCase);
    }
}
