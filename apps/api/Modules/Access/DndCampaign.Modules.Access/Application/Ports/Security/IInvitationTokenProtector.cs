namespace DndCampaign.Modules.Access.Application.Ports.Security;

internal interface IInvitationTokenProtector
{
    string Protect(string token);

    string Unprotect(string protectedToken);
}
