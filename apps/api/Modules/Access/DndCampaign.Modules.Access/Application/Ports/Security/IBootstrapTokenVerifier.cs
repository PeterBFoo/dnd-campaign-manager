namespace DndCampaign.Modules.Access.Application.Ports.Security;

internal interface IBootstrapTokenVerifier
{
    bool Matches(string candidate);
}
