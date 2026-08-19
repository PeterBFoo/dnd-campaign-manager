using System.Text.Encodings.Web;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;

namespace DndCampaign.Api.Application.Invitations;

public sealed class InvitationEmailComposer(IdentitySecurityOptions options)
{
    public TransactionalEmail Compose(
        InvitationRecord invitation,
        string token,
        string correlationId)
    {
        var invitationPath = new Uri(
            options.FrontendBaseUrl,
            $"accept-invitation#token={Uri.EscapeDataString(token)}");
        var encodedLink = HtmlEncoder.Default.Encode(invitationPath.ToString());
        var isCampaign = invitation.Kind == InvitationKind.Campaign;
        var subject = isCampaign
            ? "Invitación para unirte a una campaña"
            : "Invitación para acceder a Campaign Companion";
        var context = isCampaign
            ? "Te han invitado a participar como jugador en una campaña."
            : "Te han invitado a crear una cuenta en Campaign Companion.";
        var text = $"{context}\n\nLa invitación caduca en siete días. Ábrela aquí: {invitationPath}";
        var html = $"""
            <p>{HtmlEncoder.Default.Encode(context)}</p>
            <p>La invitación caduca en siete días.</p>
            <p><a href="{encodedLink}">Aceptar invitación</a></p>
            <p>Si no esperabas este mensaje, puedes ignorarlo.</p>
            """;

        return new TransactionalEmail(
            invitation.RecipientEmail,
            RecipientName: null,
            subject,
            text,
            html,
            isCampaign ? "campaign-invitation" : "platform-invitation",
            correlationId);
    }
}
