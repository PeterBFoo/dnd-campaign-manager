using System.Diagnostics;
using System.Text.Json;
using DndCampaign.Modules.Access;
using DndCampaign.Modules.AdventureCatalog;
using DndCampaign.Modules.Campaigns;
using DndCampaign.Modules.Characters;
using DndCampaign.Modules.Combat;
using DndCampaign.Modules.Journal;
using DndCampaign.Modules.Missions;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "dnd-campaign-api";

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddProblemDetails();
builder.Services.AddAccessModule(builder.Configuration, builder.Environment);
builder.Services.AddAdventureCatalogModule(builder.Configuration, builder.Environment);
builder.Services.AddCampaignsModule(builder.Configuration, builder.Environment);
builder.Services.AddScoped<ICampaignAdventureContext, CampaignAdventureContextAdapter>();
builder.Services.AddCharactersModule(builder.Configuration, builder.Environment);
builder.Services.AddCombatModule(builder.Configuration, builder.Environment);
builder.Services.AddJournalModule(builder.Configuration, builder.Environment);
builder.Services.AddMissionsModule(builder.Configuration, builder.Environment);
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .WithHeaders("Accept", "Content-Type", "Authorization")
        .WithExposedHeaders("X-Correlation-Id")));
}

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: serviceName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                if (request.Path.Value?.EndsWith("/eligible-users", StringComparison.Ordinal) == true)
                {
                    activity.SetTag("url.query", null);
                    activity.SetTag("http.target", request.Path.Value);
                }
            };
        })
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.AddOtlpExporter();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "D&D Campaign API v1");
        options.RoutePrefix = "swagger";
    });
}
if (allowedOrigins.Length > 0)
{
    app.UseCors("frontend");
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.TryAdd("X-Correlation-Id", correlationId);
        return Task.CompletedTask;
    });
    await next();
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync,
});
app.MapAccessModule();
app.MapAdventureCatalogModule();
app.MapCampaignsModule();
app.MapCharactersModule();
app.MapCombatModule();
app.MapJournalModule();
app.MapMissionsModule();

if (builder.Configuration.GetValue("Database:ApplyMigrations", false))
{
    await app.Services.ApplyAccessMigrationsAsync();
    await app.Services.ApplyAdventureCatalogMigrationsAsync();
    await app.Services.ApplyCampaignsMigrationsAsync();
    await app.Services.ApplyCharactersMigrationsAsync();
    await app.Services.ApplyJournalMigrationsAsync();
    await app.Services.ApplyMissionsMigrationsAsync();
    await app.Services.ApplyCombatMigrationsAsync();
}

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString().ToLowerInvariant(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString().ToLowerInvariant(),
        }),
    }));
}

public partial class Program;
