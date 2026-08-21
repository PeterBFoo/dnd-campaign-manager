using System.Security.Claims;
using DndCampaign.Api.Api;
using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class IdentityControllerTests
{
    [Fact]
    public async Task GetBootstrapStatus_returns_200_with_state()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(service => service.GetBootstrapStatus(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BootstrapStatus.Completed);
        var controller = new IdentityController(identityService.Object);

        var result = await controller.GetBootstrapStatus(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<BootstrapStatusResponse>(ok.Value);
        Assert.Equal("completed", response.State);
    }

    [Fact]
    public async Task Bootstrap_returns_201_with_created_user()
    {
        var userId = Guid.NewGuid();
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(service => service.BootstrapAsync(It.IsAny<BootstrapAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                BootstrapCreationStatus.Created,
                Array.Empty<IdentityAccountValidationErrors>(),
                new UserProfile(userId, "admin@example.com", "Platform Admin", true)));
        var controller = new IdentityController(identityService.Object);

        var result = await controller.Bootstrap(
            new BootstrapRequest("token", "admin@example.com", "Platform Admin", "A-valid-admin-password-123!"),
            TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal("/api/v1/identity/me", created.Location);
        var response = Assert.IsType<UserResponse>(created.Value);
        Assert.Equal(userId, response.Id);
        Assert.True(response.IsPlatformAdmin);
    }

    [Fact]
    public async Task Bootstrap_returns_400_with_validation_problem()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(service => service.BootstrapAsync(It.IsAny<BootstrapAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                BootstrapCreationStatus.InvalidCredentials,
                [IdentityAccountValidationErrors.PasswordTooShortOrTooLong],
                null));
        var controller = new IdentityController(identityService.Object);

        var result = await controller.Bootstrap(
            new BootstrapRequest("token", "admin@example.com", "Platform Admin", "short"),
            TestContext.Current.CancellationToken);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.True(problem.Errors.ContainsKey("password"));
    }

    [Fact]
    public async Task Bootstrap_returns_409_when_initial_registration_is_closed()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(service => service.BootstrapAsync(It.IsAny<BootstrapAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                BootstrapCreationStatus.InitialRegistrationClosed,
                Array.Empty<IdentityAccountValidationErrors>(),
                null));
        var controller = new IdentityController(identityService.Object);

        var result = await controller.Bootstrap(
            new BootstrapRequest("token", "admin@example.com", "Platform Admin", "A-valid-admin-password-123!"),
            TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task Bootstrap_returns_401_for_invalid_token()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(service => service.BootstrapAsync(It.IsAny<BootstrapAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BootstrapCreationStatus.InvalidBootstrapToken, Array.Empty<IdentityAccountValidationErrors>(), null));
        var controller = new IdentityController(identityService.Object);

        var result = await controller.Bootstrap(
            new BootstrapRequest("bad", "a@b.com", "Name", "password"),
            TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_returns_401_when_credentials_are_invalid()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginOutcome?)null);
        var controller = new IdentityController(identityService.Object);

        var result = await controller.Login(
            new LoginRequest("a@b.com", "wrong"),
            TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_returns_200_with_session()
    {
        var userId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginOutcome(
                "access-token",
                expiresAt,
                new UserProfile(userId, "admin@example.com", "Platform Admin", true)));
        var controller = new IdentityController(identityService.Object);

        var result = await controller.Login(
            new LoginRequest("admin@example.com", "A-valid-admin-password-123!"),
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SessionResponse>(ok.Value);
        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal(expiresAt, response.ExpiresAt);
        Assert.Equal(userId, response.User.Id);
    }

    [Fact]
    public async Task Logout_returns_204_and_revokes_the_session_from_claims()
    {
        var sessionId = Guid.NewGuid();
        var identityService = new Mock<IIdentityService>();
        var controller = new IdentityController(identityService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim("session_id", sessionId.ToString()),
                ], authenticationType: "test")),
            },
        };

        var result = await controller.Logout(TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        identityService.Verify(
            service => service.LogoutAsync(
                It.Is<LogoutCommand>(command => command.SessionId == sessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Me_returns_200_with_user_response_from_claims()
    {
        var userId = Guid.NewGuid();
        var controller = new IdentityController(Mock.Of<IIdentityService>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Email, "player@example.com"),
                    new Claim(ClaimTypes.Name, "Player"),
                    new Claim("platform_admin", "true"),
                ], authenticationType: "test")),
            },
        };

        var result = controller.Me();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserResponse>(ok.Value);
        Assert.Equal(userId, response.Id);
        Assert.Equal("player@example.com", response.Email);
        Assert.True(response.IsPlatformAdmin);
    }
}
