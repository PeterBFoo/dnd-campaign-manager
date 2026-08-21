using DndCampaign.Api.Api;
using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class InvitationAcceptanceControllerTests
{
    private static InvitationAcceptanceController CreateController(Mock<IInvitationAcceptanceService> service)
    {
        var controller = new InvitationAcceptanceController(service.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    [Fact]
    public async Task Preview_returns_200_with_outcome()
    {
        var acceptanceService = new Mock<IInvitationAcceptanceService>();
        acceptanceService
            .Setup(service => service.PreviewAsync(It.IsAny<PreviewInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvitationPreviewOutcome("valid", "platform", "pl***@example.com", DateTimeOffset.UtcNow, false));
        var controller = CreateController(acceptanceService);

        var result = await controller.Preview(
            new InvitationTokenRequest("token-with-at-least-32-characters-long"),
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InvitationPreviewResponse>(ok.Value);
        Assert.Equal("valid", response.State);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("accepted")]
    [InlineData("invalid")]
    public async Task Preview_returns_200_for_non_valid_states(string state)
    {
        var acceptanceService = new Mock<IInvitationAcceptanceService>();
        acceptanceService
            .Setup(service => service.PreviewAsync(It.IsAny<PreviewInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvitationPreviewOutcome(state, null, null, null, false));
        var controller = CreateController(acceptanceService);

        var result = await controller.Preview(
            new InvitationTokenRequest("token-with-at-least-32-characters-long"),
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InvitationPreviewResponse>(ok.Value);
        Assert.Equal(state, response.State);
        Assert.Null(response.Kind);
    }

    [Theory]
    [InlineData(AcceptInvitationStatus.NotFound, 410)]
    [InlineData(AcceptInvitationStatus.Expired, 410)]
    [InlineData(AcceptInvitationStatus.AlreadyAccepted, 410)]
    [InlineData(AcceptInvitationStatus.Unauthorized, 401)]
    [InlineData(AcceptInvitationStatus.Forbidden, 403)]
    public async Task Accept_maps_failure_status_codes(AcceptInvitationStatus status, int expectedStatusCode)
    {
        var acceptanceService = new Mock<IInvitationAcceptanceService>();
        acceptanceService
            .Setup(service => service.AcceptAsync(It.IsAny<AcceptInvitationCommand>(), It.IsAny<AuthenticatedActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AcceptInvitationResult.Failure(status));
        var controller = CreateController(acceptanceService);

        var result = await controller.Accept(
            new AcceptInvitationRequest("token-with-at-least-32-characters-long", null, null),
            TestContext.Current.CancellationToken);

        switch (expectedStatusCode)
        {
            case 401:
                Assert.IsType<UnauthorizedResult>(result);
                break;
            case 403:
                Assert.IsType<ForbidResult>(result);
                break;
            case 410:
                var statusCode = Assert.IsType<ObjectResult>(result);
                Assert.Equal(410, statusCode.StatusCode);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedStatusCode));
        }
    }

    [Fact]
    public async Task Accept_returns_400_for_invalid_credentials()
    {
        var acceptanceService = new Mock<IInvitationAcceptanceService>();
        acceptanceService
            .Setup(service => service.AcceptAsync(It.IsAny<AcceptInvitationCommand>(), It.IsAny<AuthenticatedActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AcceptInvitationResult.InvalidCredentials([IdentityAccountValidationErrors.DisplayName]));
        var controller = CreateController(acceptanceService);

        var result = await controller.Accept(
            new AcceptInvitationRequest("token-with-at-least-32-characters-long", "Player", "weak"),
            TestContext.Current.CancellationToken);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.True(problem.Errors.ContainsKey("displayName"));
    }

    [Fact]
    public async Task Accept_returns_200_on_success()
    {
        var acceptanceService = new Mock<IInvitationAcceptanceService>();
        acceptanceService
            .Setup(service => service.AcceptAsync(It.IsAny<AcceptInvitationCommand>(), It.IsAny<AuthenticatedActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AcceptInvitationResult.Success(
                new InvitationAcceptanceOutcome(
                    new UserProfile(Guid.NewGuid(), "player@example.com", "Player", false),
                    "access-token",
                    DateTimeOffset.UtcNow.AddHours(8),
                    "platform")));
        var controller = CreateController(acceptanceService);

        var result = await controller.Accept(
            new AcceptInvitationRequest("token-with-at-least-32-characters-long", "Player", "A-valid-password-123!"),
            TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result);
    }
}
