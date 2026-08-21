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

    [Fact]
    public async Task Cors_allows_the_configured_frontend_only()
    {
        const string frontendOrigin = "https://peterbfoo.github.io";
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("OTEL_SDK_DISABLED", "true");
                builder.UseSetting(
                    "ConnectionStrings:Campaigns",
                    "Host=localhost;Port=5432;Database=test;Username=test;Password=test-only");
                builder.UseSetting("Cors:AllowedOrigins:0", frontendOrigin);
            });
        using var corsClient = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", frontendOrigin);

        using var response = await corsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(frontendOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task PostgreSql_uri_is_accepted_as_a_connection_string()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("OTEL_SDK_DISABLED", "true");
                builder.UseSetting(
                    "ConnectionStrings:Campaigns",
                    "postgresql://test-user:test%3Apassword@localhost:5432/test-db?sslmode=require");
            });
        using var uriClient = factory.CreateClient();

        using var response = await uriClient.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_is_unhealthy_when_postgres_is_unavailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/health/ready", cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("unhealthy", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Platform_status_is_degraded_when_postgres_is_unavailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/api/v1/platform/status", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("\"status\":\"degraded\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"database\":\"unavailable\"", payload, StringComparison.Ordinal);
    }
}
