using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DndCampaign.Modules.Access.Tests.Component;

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
    public async Task Swagger_documents_access_controllers_in_development()
    {
        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("/api/v1/identity/bootstrap", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/invitations/accept", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/platform/invitations", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/campaigns", document, StringComparison.Ordinal);
        Assert.Contains("eligible-users", document, StringComparison.Ordinal);

        using var ui = await client.GetAsync(
            "/swagger/index.html",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    [Fact]
    public async Task Swagger_is_not_exposed_outside_development()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("OTEL_SDK_DISABLED", "true");
                builder.UseSetting(
                    "ConnectionStrings:Campaigns",
                    "Host=localhost;Port=5432;Database=test;Username=test;Password=test-only");
                builder.UseSetting(
                    "Identity:BootstrapToken",
                    "production-test-bootstrap-token-with-32-characters");
                builder.UseSetting(
                    "Identity:OutboxEncryptionKey",
                    "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
                builder.UseSetting("Frontend:BaseUrl", "https://example.com/application/");
            });
        using var productionClient = factory.CreateClient();

        using var response = await productionClient.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
