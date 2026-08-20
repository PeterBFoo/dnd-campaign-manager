using DndCampaign.Api.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DndCampaign.Api.Api;

[ApiController]
[Route("api/v1/identity")]
public sealed class IdentityController(IdentityService identityService, ILogger<IdentityController> logger)
    : ControllerBase
{
    private readonly ILogger<IdentityController> _logger = logger;
    private readonly IdentityService _identityService = identityService;

    [HttpGet("bootstrap")]
    public async Task<IActionResult> GetBootstrapStatus(CancellationToken cancellationToken)
    {
        var bootstrapStatus = await _identityService.GetBootstrapStatus(cancellationToken);
        return Ok(new
        {
            state = bootstrapStatus == BootstrapStatus.Completed ? "completed" : "required"
        });
    }

    [HttpPost("bootstrap")]
    [EnableRateLimiting("bootstrap")]
    public async Task<IActionResult> Bootstrap([FromBody] BootstrapRequest request, CancellationToken cancellationToken)
    {
        var (status, errors, userResponse) = await _identityService.BootstrapAsync(request, cancellationToken);
        return status switch
        {
            BootstrapCreationStatus.InvalidBootstrapToken => CannotValidateCredentials(),
            BootstrapCreationStatus.InvalidCredentials => InvalidCredentials(errors),
            BootstrapCreationStatus.InitialRegistrationClosed => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "El alta inicial ya está cerrada.",
                Detail = "La primera cuenta de administración ya fue creada."
            }),
            BootstrapCreationStatus.Created => Created("/api/v1/identity/me", userResponse),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    private IActionResult CannotValidateCredentials()
    {
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "No se han podido validar las credenciales."
        });
    }

    private IActionResult InvalidCredentials(
        IEnumerable<IdentityAccountValidationErrors> errors)
    {
        return BadRequest(
            IdentityValidationProblemFactory.Create(errors));
    }
}