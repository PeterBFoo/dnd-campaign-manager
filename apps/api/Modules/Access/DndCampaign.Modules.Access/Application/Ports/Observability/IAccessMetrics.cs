namespace DndCampaign.Modules.Access.Application.Ports.Observability;

internal interface IAccessMetrics
{
    void BootstrapCompleted();

    void LoginAttempted();

    void LoginFailed();

    void InvitationIssued(InvitationOperation operation, string kind);

    void InvitationAccepted(string kind);

    void InvitationRevoked(string kind);
}

internal enum InvitationOperation
{
    Initial,
    Resend,
}
