using System.Diagnostics.Metrics;
using DndCampaign.Modules.Access.Application.Ports.Observability;

namespace DndCampaign.Modules.Access.Infrastructure.Observability;

internal sealed class AccessMetrics : IAccessMetrics
{
    public const string MeterName = "DndCampaign.Api.Identity";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> BootstrapCompletions =
        Meter.CreateCounter<long>("identity.bootstrap.completions");
    private static readonly Counter<long> LoginAttempts = Meter.CreateCounter<long>("identity.login.attempts");
    private static readonly Counter<long> LoginFailures = Meter.CreateCounter<long>("identity.login.failures");
    private static readonly Counter<long> InvitationsIssued = Meter.CreateCounter<long>("identity.invitations.issued");
    private static readonly Counter<long> InvitationsAccepted = Meter.CreateCounter<long>("identity.invitations.accepted");
    private static readonly Counter<long> InvitationsRevoked = Meter.CreateCounter<long>("identity.invitations.revoked");

    public void BootstrapCompleted() => BootstrapCompletions.Add(1);

    public void LoginAttempted() => LoginAttempts.Add(1);

    public void LoginFailed() => LoginFailures.Add(1);

    public void InvitationIssued(InvitationOperation operation, string kind) => InvitationsIssued.Add(
        1,
        new KeyValuePair<string, object?>("invitation.kind", kind),
        new KeyValuePair<string, object?>("invitation.operation", operation.ToString().ToLowerInvariant()));

    public void InvitationAccepted(string kind) => InvitationsAccepted.Add(
        1,
        new KeyValuePair<string, object?>("invitation.kind", kind));

    public void InvitationRevoked(string kind) => InvitationsRevoked.Add(
        1,
        new KeyValuePair<string, object?>("invitation.kind", kind));
}
