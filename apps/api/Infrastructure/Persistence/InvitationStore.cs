using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Domain.Invitations;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Infrastructure.Persistence;

public sealed class InvitationStore(CampaignDbContext database) : IInvitationStore
{
    public Task<bool> HasPendingAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        database.Invitations.AsNoTracking().AnyAsync(
            invitation =>
                invitation.Kind == kind
                && invitation.CampaignId == campaignId
                && invitation.RecipientEmail == recipientEmail
                && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAt > now,
            cancellationToken);

    public async Task<Invitation?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        var record = await database.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancellationToken);
        return record is null ? null : InvitationPersistenceMapping.ToDomain(record);
    }

    public async Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var record = await database.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(invitation => invitation.Id == invitationId, cancellationToken);
        return record is null ? null : InvitationPersistenceMapping.ToDomain(record);
    }

    public async Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken)
    {
        var record = await database.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invitation =>
                    invitation.Id == invitationId
                    && invitation.Kind == kind
                    && invitation.CampaignId == campaignId,
                cancellationToken);
        return record is null ? null : InvitationPersistenceMapping.ToDomain(record);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> ListRecentIssueTimesAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        await database.Invitations
            .AsNoTracking()
            .Where(invitation =>
                invitation.Kind == kind
                && invitation.CampaignId == campaignId
                && invitation.RecipientEmail == recipientEmail
                && invitation.IssuedAt >= since)
            .Select(invitation => invitation.IssuedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InvitationListItem>> ListAsync(
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken)
    {
        var records = await database.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.Kind == kind && invitation.CampaignId == campaignId)
            .OrderByDescending(invitation => invitation.IssuedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return records
            .Select(record => new InvitationListItem(InvitationPersistenceMapping.ToDomain(record), record.LastSentAt))
            .ToArray();
    }

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        var record = InvitationPersistenceMapping.ToRecord(invitation);
        database.Invitations.Add(record);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, record);
    }

    public async Task SaveAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        var record = await database.Invitations.SingleAsync(
            candidate => candidate.Id == invitation.Id,
            cancellationToken);
        record.ApplyBusinessState(invitation);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, record);
    }

    public async Task SaveAllAsync(
        IReadOnlyCollection<Invitation> invitations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitations);
        if (invitations.Count == 0)
        {
            return;
        }

        var expected = invitations.ToDictionary(invitation => invitation.Id);
        var invitationIds = expected.Keys.ToArray();
        var records = await database.Invitations
            .Where(record => invitationIds.Contains(record.Id))
            .ToListAsync(cancellationToken);
        if (records.Count != expected.Count)
        {
            throw new InvalidOperationException("One or more invitations could not be found for batch persistence.");
        }

        foreach (var record in records)
        {
            record.ApplyBusinessState(expected[record.Id]);
        }

        await CampaignDbContextPersistence.SaveEntitiesAsync(
            database,
            records.Cast<object>().ToArray(),
            cancellationToken);
    }

    public async Task MarkSentAsync(Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var record = await database.Invitations.SingleAsync(
            candidate => candidate.Id == invitationId,
            cancellationToken);
        record.MarkSent(now);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, record);
    }
}
