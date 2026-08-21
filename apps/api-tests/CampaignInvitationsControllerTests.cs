using System.Security.Claims;
using DndCampaign.Api.Api;
using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class CampaignInvitationsControllerTests
{
    private static CampaignInvitationsController CreateController(Mock<ICampaignInvitationService> service)
    {
        var controller = new CampaignInvitationsController(service.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                ], authenticationType: "test")),
            },
        };
        return controller;
    }

    [Fact]
    public async Task Issue_returns_202_when_allowed()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssueCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Allowed, new InvitationSummary(
                Guid.NewGuid(), "campaign", "player@example.com", Guid.NewGuid(),
                "pending", "pending", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7), null)));
        var controller = CreateController(service);

        var result = await controller.Issue(
            Guid.NewGuid(),
            new IssueInvitationRequest("player@example.com"),
            TestContext.Current.CancellationToken);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.IsType<InvitationResponse>(accepted.Value);
    }

    [Fact]
    public async Task Issue_returns_403_when_access_forbidden()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssueCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Forbidden, null));
        var controller = CreateController(service);

        var result = await controller.Issue(
            Guid.NewGuid(),
            new IssueInvitationRequest("player@example.com"),
            TestContext.Current.CancellationToken);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Revoke_returns_204_when_revoked()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.RevokeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RevokeInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Allowed, RevokeInvitationStatus.Revoked));
        var controller = CreateController(service);

        var result = await controller.Revoke(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Revoke_returns_404_when_not_found()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.RevokeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RevokeInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Allowed, RevokeInvitationStatus.NotFound));
        var controller = CreateController(service);

        var result = await controller.Revoke(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Revoke_returns_409_when_invitation_is_no_longer_pending()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.RevokeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RevokeInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Allowed, RevokeInvitationStatus.Conflict));
        var controller = CreateController(service);

        var result = await controller.Revoke(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task Issue_propagates_conflict_exception_for_exception_handler()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.IssueAsync(It.IsAny<IssueCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationConflictException());
        var controller = CreateController(service);

        await Assert.ThrowsAsync<InvitationConflictException>(() =>
            controller.Issue(
                Guid.NewGuid(),
                new IssueInvitationRequest("player@example.com"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Resend_returns_202_when_resent()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Allowed, ResendInvitationStatus.Resent, new InvitationSummary(
                Guid.NewGuid(), "campaign", "player@example.com", Guid.NewGuid(),
                "pending", "pending", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow)));
        var controller = CreateController(service);

        var result = await controller.Resend(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.IsType<InvitationResponse>(accepted.Value);
    }

    [Fact]
    public async Task Resend_returns_404_when_invitation_missing()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Allowed, ResendInvitationStatus.NotFound, null));
        var controller = CreateController(service);

        var result = await controller.Resend(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Resend_returns_403_when_access_forbidden()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Forbidden, ResendInvitationStatus.NotFound, null));
        var controller = CreateController(service);

        var result = await controller.Resend(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Resend_propagates_rate_limit_exception_for_exception_handler()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.ResendAsync(It.IsAny<ResendCampaignInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvitationRateLimitException(DateTimeOffset.UtcNow.AddMinutes(15)));
        var controller = CreateController(service);

        await Assert.ThrowsAsync<InvitationRateLimitException>(() =>
            controller.Resend(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task List_returns_200_with_invitation_responses()
    {
        var service = new Mock<ICampaignInvitationService>();
        service
            .Setup(s => s.ListAsync(It.IsAny<ListCampaignInvitationsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CampaignAccessStatus.Allowed, new List<InvitationSummary>
            {
                new(Guid.NewGuid(), "campaign", "player@example.com", Guid.NewGuid(),
                    "pending", "pending", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7), null),
            }));
        var controller = CreateController(service);

        var result = await controller.List(Guid.NewGuid(), TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var responses = Assert.IsAssignableFrom<IReadOnlyList<InvitationResponse>>(ok.Value);
        Assert.Single(responses);
    }
}
