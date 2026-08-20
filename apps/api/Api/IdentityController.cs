using DndCampaign.Api.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Api.Api.Controllers;

[ApiController]
[Route("api/v1/identity")]
public sealed class IdentityController(IdentityService identityService, ILogger<IdentityController> logger) : ControllerBase
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
}