using System.Diagnostics.Metrics;

namespace DndCampaign.Api.Infrastructure.Observability;

public static class IdentityTelemetry
{
    public const string MeterName = "DndCampaign.Api.Identity";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> BootstrapCompletions =
        Meter.CreateCounter<long>("identity.bootstrap.completions");

    public static readonly Counter<long> LoginAttempts =
        Meter.CreateCounter<long>("identity.login.attempts");

    public static readonly Counter<long> LoginFailures =
        Meter.CreateCounter<long>("identity.login.failures");

    public static readonly Counter<long> InvitationsIssued =
        Meter.CreateCounter<long>("identity.invitations.issued");

    public static readonly Counter<long> InvitationsAccepted =
        Meter.CreateCounter<long>("identity.invitations.accepted");

    public static readonly Counter<long> InvitationsRevoked =
        Meter.CreateCounter<long>("identity.invitations.revoked");
}
