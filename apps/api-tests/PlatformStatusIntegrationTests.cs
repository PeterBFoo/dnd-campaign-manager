using System.Net;
using System.Text.Json;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DndCampaign.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class PlatformStatusIntegrationTests
{
    [Fact]
    public async Task Platform_status_is_operational_when_postgres_is_available()
    {
        using var factory = await CreateFactoryAsync(TestContext.Current.CancellationToken);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/platform/status",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(payload);
        Assert.Equal("operational", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "connected",
            json.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Ready_is_healthy_when_postgres_is_available()
    {
        using var factory = await CreateFactoryAsync(TestContext.Current.CancellationToken);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("healthy", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<WebApplicationFactory<Program>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_SDK_DISABLED", "true");
            builder.UseSetting("ConnectionStrings:Campaigns", connectionString);
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Email:OutboxWorkerEnabled", "false");
            builder.UseSetting("Identity:BootstrapToken", "integration-bootstrap-token-with-32-characters");
            builder.UseSetting(
                "Identity:OutboxEncryptionKey",
                "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
            builder.UseSetting("Frontend:BaseUrl", "https://example.com/application/");
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
        await database.Database.EnsureDeletedAsync(cancellationToken);
        await database.Database.MigrateAsync(cancellationToken);
        return factory;
    }
}
