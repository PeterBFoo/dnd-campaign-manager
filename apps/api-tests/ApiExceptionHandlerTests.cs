using System.Net;
using System.Text.Json;
using DndCampaign.Api.Api.Middleware;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task Invitation_conflict_maps_to_409_problem_details()
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new InvitationConflictException(),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json; charset=utf-8", context.Response.ContentType);
        var json = await ReadJsonAsync(context);
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Ya existe una invitación pendiente.", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invitation_rate_limit_maps_to_429_with_retry_at()
    {
        var context = CreateContext();
        var retryAt = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new InvitationRateLimitException(retryAt),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal((int)HttpStatusCode.TooManyRequests, context.Response.StatusCode);
        Assert.Equal("application/problem+json; charset=utf-8", context.Response.ContentType);
        var json = await ReadJsonAsync(context);
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(retryAt, json.RootElement.GetProperty("retryAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Invitation_state_exception_maps_to_409_problem_details()
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new InvitationStateException("Solo se puede reenviar una invitación pendiente."),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        var json = await ReadJsonAsync(context);
        Assert.Equal("La invitación no puede reenviarse.", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invitation_email_validation_maps_to_400_validation_problem()
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new InvitationEmailValidationException("The email address is invalid."),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal("application/problem+json; charset=utf-8", context.Response.ContentType);
        var json = await ReadJsonAsync(context);
        Assert.Equal("One or more validation errors occurred.", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "The email address is invalid.",
            json.RootElement.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Generic_argument_exception_is_not_handled()
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new ArgumentException("Unrelated validation failure."),
            TestContext.Current.CancellationToken);

        Assert.False(handled);
    }

    private static ApiExceptionHandler CreateHandler() =>
        new(NullLogger<ApiExceptionHandler>.Instance);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
