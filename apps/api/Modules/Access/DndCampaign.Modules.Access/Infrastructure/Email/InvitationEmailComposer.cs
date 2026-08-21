using System.Text.Encodings.Web;
using DndCampaign.Modules.Access.Domain.Invitations;
using DndCampaign.Modules.Access.Application.Ports.Email;
using DndCampaign.Modules.Access.Infrastructure.Security;

namespace DndCampaign.Modules.Access.Infrastructure.Email;

internal sealed class InvitationEmailComposer(AccessSecurityOptions options)
{
    public TransactionalEmail Compose(
        Invitation invitation,
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
