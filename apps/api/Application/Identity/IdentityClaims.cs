using System.Security.Claims;

namespace DndCampaign.Api.Application.Identity;

public static class IdentityClaims
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : throw new InvalidOperationException("The authenticated identity has no valid user identifier.");
    }

    public static Guid GetSessionId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("session_id");
        return Guid.TryParse(value, out var sessionId) ? sessionId : throw new InvalidOperationException("The authenticated identity has no valid session identifier.");
    }
}
