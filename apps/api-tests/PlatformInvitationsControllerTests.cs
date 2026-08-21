using System.Security.Claims;
using DndCampaign.Api.Api;
using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class PlatformInvitationsControllerTests
{
    private static PlatformInvitationsController CreateController(Mock<IPlatformInvitationService> service)
    {
        var controller = new PlatformInvitationsController(service.Object);
        var userId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                ], authenticationType: "test")),
            },
        };
        return controller;
    }

    [Fact]
    public async Task Issue_returns_202_accepted()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssuePlatformInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvitationSummary(
                Guid.NewGuid(), "platform", "player@example.com", null,
                "pending", InvitationDeliveryStatus.Pending, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7), null));
        var controller = CreateController(service);

        var result = await controller.Issue(
            new IssueInvitationRequest("player@example.com"),
            TestContext.Current.CancellationToken);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.IsType<InvitationResponse>(accepted.Value);
    }

    [Theory]
    [InlineData(InvitationDeliveryStatus.Pending, "pending")]
    [InlineData(InvitationDeliveryStatus.Sent, "sent")]
    [InlineData(InvitationDeliveryStatus.Discarded, "discarded")]
    [InlineData(InvitationDeliveryStatus.Failed, "failed")]
    public async Task Issue_maps_delivery_status_to_http_literals(
        InvitationDeliveryStatus deliveryStatus,
        string expected)
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssuePlatformInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvitationSummary(
                Guid.NewGuid(), "platform", "player@example.com", null,
                "pending", deliveryStatus, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7), null));
        var controller = CreateController(service);

        var result = await controller.Issue(
            new IssueInvitationRequest("player@example.com"),
            TestContext.Current.CancellationToken);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var response = Assert.IsType<InvitationResponse>(accepted.Value);
        Assert.Equal(expected, response.DeliveryStatus);
    }

    [Fact]
    public async Task Issue_propagates_conflict_exception_for_exception_handler()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssuePlatformInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationConflictException());
        var controller = CreateController(service);

        await Assert.ThrowsAsync<InvitationConflictException>(() =>
            controller.Issue(new IssueInvitationRequest("player@example.com"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Resend_returns_404_when_invitation_missing()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResendInvitationStatus.NotFound, null));
        var controller = CreateController(service);

        var result = await controller.Resend(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Resend_propagates_rate_limit_exception_for_exception_handler()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationRateLimitException(DateTimeOffset.UtcNow.AddMinutes(15)));
        var controller = CreateController(service);

        await Assert.ThrowsAsync<InvitationRateLimitException>(() =>
            controller.Resend(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Revoke_returns_204_when_revoked()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.RevokeAsync(It.IsAny<RevokeInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RevokeInvitationStatus.Revoked);
        var controller = CreateController(service);

        var result = await controller.Revoke(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Revoke_returns_404_when_not_found()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.RevokeAsync(It.IsAny<RevokeInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RevokeInvitationStatus.NotFound);
        var controller = CreateController(service);

        var result = await controller.Revoke(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Revoke_returns_409_when_invitation_is_no_longer_pending()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.RevokeAsync(It.IsAny<RevokeInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RevokeInvitationStatus.Conflict);
        var controller = CreateController(service);

        var result = await controller.Revoke(Guid.NewGuid(), TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task Resend_returns_202_when_resent()
    {
        var service = new Mock<IPlatformInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResendInvitationStatus.Resent, new InvitationSummary(
                Guid.NewGuid(), "platform", "player@example.com", null,
                "pending", InvitationDeliveryStatus.Pending, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow)));
        var controller = CreateController(service);

        var result = await controller.Resend(Guid.NewGuid(), TestContext.Current.CancellationToken);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.IsType<InvitationResponse>(accepted.Value);
    }
}
