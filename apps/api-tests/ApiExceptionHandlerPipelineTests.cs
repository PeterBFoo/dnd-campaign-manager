using System.Net;
using System.Security.Claims;
using System.Text.Json;
using DndCampaign.Api.Api;
using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Api.Middleware;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class ApiExceptionHandlerPipelineTests
{
    [Fact]
    public async Task Platform_issue_conflict_returns_409_problem_details()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssuePlatformInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationConflictException());

        using var host = await StartHostAsync(context =>
            InvokeAsync(context, new PlatformInvitationsController(service.Object), controller =>
                controller.Issue(new IssueInvitationRequest("player@example.com"), context.RequestAborted)));

        var response = await host.GetTestClient().PostAsync(
            "/platform/invitations",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "Ya existe una invitación pendiente.",
            problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Platform_resend_rate_limit_returns_429_with_retry_at()
    {
        var retryAt = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationRateLimitException(retryAt));

        using var host = await StartHostAsync(context =>
            InvokeAsync(context, new PlatformInvitationsController(service.Object), controller =>
                controller.Resend(Guid.NewGuid(), context.RequestAborted)));

        var response = await host.GetTestClient().PostAsync(
            "/platform/invitations/resend",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(retryAt, problem.RootElement.GetProperty("retryAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Platform_issue_invalid_email_returns_400_validation_problem()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssuePlatformInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationEmailValidationException("The email address is invalid."));

        using var host = await StartHostAsync(context =>
            InvokeAsync(context, new PlatformInvitationsController(service.Object), controller =>
                controller.Issue(new IssueInvitationRequest("not-an-email"), context.RequestAborted)));

        var response = await host.GetTestClient().PostAsync(
            "/platform/invitations",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(
            "The email address is invalid.",
            problem.RootElement.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Campaign_issue_conflict_returns_409_problem_details()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssueCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationConflictException());

        using var host = await StartHostAsync(context =>
            InvokeAsync(context, new CampaignInvitationsController(service.Object), controller =>
                controller.Issue(
                    Guid.NewGuid(),
                    new IssueInvitationRequest("player@example.com"),
                    context.RequestAborted)));

        var response = await host.GetTestClient().PostAsync(
            "/campaigns/invitations",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Campaign_resend_rate_limit_returns_429_with_retry_at()
    {
        var retryAt = new DateTimeOffset(2026, 8, 21, 18, 30, 0, TimeSpan.Zero);
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationRateLimitException(retryAt));

        using var host = await StartHostAsync(context =>
            InvokeAsync(context, new CampaignInvitationsController(service.Object), controller =>
                controller.Resend(Guid.NewGuid(), Guid.NewGuid(), context.RequestAborted)));

        var response = await host.GetTestClient().PostAsync(
            "/campaigns/invitations/resend",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(retryAt, problem.RootElement.GetProperty("retryAt").GetDateTimeOffset());
    }

    private static Task InvokeAsync<TController>(
        HttpContext context,
        TController controller,
        Func<TController, Task<IActionResult>> action)
        where TController : ControllerBase
    {
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        ], authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return action(controller);
    }

    private static async Task<IHost> StartHostAsync(Func<HttpContext, Task> handler) =>
        await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddProblemDetails();
                    services.AddExceptionHandler<ApiExceptionHandler>();
                })
                .Configure(app =>
                {
                    app.UseExceptionHandler();
                    app.Run(context => handler(context));
                }))
            .StartAsync(TestContext.Current.CancellationToken);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
}
