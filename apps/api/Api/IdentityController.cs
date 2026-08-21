using System.Security.Claims;
using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DndCampaign.Api.Api;

[ApiController]
[Route("api/v1/identity")]
public sealed class IdentityController(IIdentityService identityService) : ControllerBase
{
    [HttpGet("bootstrap")]
    public async Task<IActionResult> GetBootstrapStatus(CancellationToken cancellationToken)
    {
        var status = await identityService.GetBootstrapStatus(cancellationToken);
        return Ok(IdentityHttpMapping.ToResponse(status));
    }

    [HttpPost("bootstrap")]
    [EnableRateLimiting("bootstrap")]
    public async Task<IActionResult> Bootstrap(
        [FromBody] BootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var (status, errors, user) = await identityService.BootstrapAsync(
            IdentityHttpMapping.ToCommand(request),
            cancellationToken);
        return status switch
        {
            BootstrapCreationStatus.InvalidBootstrapToken => CannotValidateCredentials(),
            BootstrapCreationStatus.InvalidCredentials => InvalidCredentials(errors),
            BootstrapCreationStatus.InitialRegistrationClosed => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "El alta inicial ya está cerrada.",
                Detail = "La primera cuenta de administración ya fue creada.",
            }),
            BootstrapCreationStatus.Created => Created("/api/v1/identity/me", IdentityHttpMapping.ToResponse(user!)),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await identityService.LoginAsync(
            IdentityHttpMapping.ToCommand(request),
            cancellationToken);
        return outcome is null
            ? CannotValidateCredentials()
            : Ok(IdentityHttpMapping.ToSessionResponse(outcome));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await identityService.LogoutAsync(IdentityHttpMapping.ToCommand(User), cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(IdentityHttpMapping.ToResponse(User));

    private IActionResult CannotValidateCredentials() =>
        Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "No se han podido validar las credenciales.",
        });

    private IActionResult InvalidCredentials(IEnumerable<IdentityAccountValidationErrors> errors) =>
        BadRequest(IdentityValidationProblemFactory.Create(errors));
}
