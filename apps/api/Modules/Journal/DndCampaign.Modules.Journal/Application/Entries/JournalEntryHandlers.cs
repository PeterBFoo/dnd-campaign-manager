using System.Diagnostics;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Journal.Application.Abstractions;
using DndCampaign.Modules.Journal.Application.Ports;
using DndCampaign.Modules.Journal.Domain.Entries;

namespace DndCampaign.Modules.Journal.Application.Entries;

internal sealed record ListJournalEntriesQuery(Guid UserId, Guid CampaignId, string? Cursor, int? Limit);
internal sealed record CreateJournalEntryCommand(Guid UserId, Guid CampaignId, string? Content);
internal sealed record UpdateJournalEntryCommand(Guid UserId, Guid CampaignId, Guid EntryId, string? Content);
internal sealed record DeleteJournalEntryCommand(Guid UserId, Guid CampaignId, Guid EntryId);

internal sealed record JournalEntryDto(
    Guid Id,
    Guid CampaignId,
    Guid AuthorCharacterId,
    string AuthorCharacterName,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool CanEdit,
    bool CanDelete);

internal sealed record JournalEntriesPageDto(IReadOnlyList<JournalEntryDto> Items, string? NextCursor);

internal sealed class ListJournalEntriesHandler(
    ICampaignAccessReader campaignAccess,
    IJournalEntryRepository entries,
    IJournalCursorCodec cursorCodec,
    IJournalMetrics metrics)
{
    public async Task<JournalResult<JournalEntriesPageDto>> HandleAsync(
        ListJournalEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await JournalAuthorization.AuthorizeAsync(
                campaignAccess, query.CampaignId, query.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = JournalErrors.Outcome(access.Error);
                return JournalResult<JournalEntriesPageDto>.Failure(access.Error);
            }

            var limit = query.Limit ?? 20;
            if (limit is < 1 or > 50)
            {
                outcome = "validation";
                return JournalResult<JournalEntriesPageDto>.Failure(JournalErrors.InvalidLimit());
            }

            JournalPageCursor? cursor = null;
            if (query.Cursor is not null && !cursorCodec.TryDecode(query.Cursor, out cursor))
            {
                outcome = "validation";
                return JournalResult<JournalEntriesPageDto>.Failure(JournalErrors.InvalidCursor());
            }

            var page = await entries.ListPageAsync(query.CampaignId, cursor, limit + 1, cancellationToken);
            var hasMore = page.Count > limit;
            var visible = page.Take(limit).ToArray();
            var nextCursor = hasMore && visible.Length > 0
                ? cursorCodec.Encode(new JournalPageCursor(
                    visible[^1].CreatedAt, visible[^1].PaginationSequence))
                : null;
            var canEdit = access.Role == CampaignRole.Player;
            var result = visible.Select(entry => JournalMapping.ToDto(
                entry, canEdit, canEdit && entry.CreatedByUserId == query.UserId)).ToArray();
            outcome = "success";
            return JournalResult<JournalEntriesPageDto>.Success(new JournalEntriesPageDto(result, nextCursor));
        }
        finally
        {
            metrics.OperationCompleted("list", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class CreateJournalEntryHandler(
    ICampaignAccessReader campaignAccess,
    IActiveCharacterReader activeCharacters,
    IJournalEntryRepository entries,
    IJournalMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<JournalResult<JournalEntryDto>> HandleAsync(
        CreateJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await JournalAuthorization.AuthorizePlayerAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access is not null)
            {
                outcome = JournalErrors.Outcome(access);
                return JournalResult<JournalEntryDto>.Failure(access);
            }

            var character = await activeCharacters.GetActiveAsync(
                command.CampaignId, command.UserId, cancellationToken);
            if (character is null)
            {
                outcome = "conflict";
                return JournalResult<JournalEntryDto>.Failure(JournalErrors.ActiveCharacterRequired());
            }

            try
            {
                var entry = JournalEntry.Create(
                    command.CampaignId,
                    command.UserId,
                    character.CharacterId,
                    character.Name,
                    command.Content!,
                    timeProvider.GetUtcNow());
                entries.Add(entry);
                await entries.SaveChangesAsync(cancellationToken);
                outcome = "success";
                return JournalResult<JournalEntryDto>.Success(JournalMapping.ToDto(entry, true, true));
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return JournalResult<JournalEntryDto>.Failure(JournalErrors.InvalidContent(exception.Message));
            }
        }
        finally
        {
            metrics.OperationCompleted("create", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class UpdateJournalEntryHandler(
    ICampaignAccessReader campaignAccess,
    IJournalEntryRepository entries,
    IJournalMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<JournalResult<JournalEntryDto>> HandleAsync(
        UpdateJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await JournalAuthorization.AuthorizePlayerAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access is not null)
            {
                outcome = JournalErrors.Outcome(access);
                return JournalResult<JournalEntryDto>.Failure(access);
            }

            var entry = await entries.FindForUpdateAsync(
                command.CampaignId, command.EntryId, cancellationToken);
            if (entry is null)
            {
                outcome = "not_found";
                return JournalResult<JournalEntryDto>.Failure(JournalErrors.EntryNotFound());
            }

            try
            {
                entry.UpdateContent(command.Content!, timeProvider.GetUtcNow());
                await entries.SaveChangesAsync(cancellationToken);
                outcome = "success";
                return JournalResult<JournalEntryDto>.Success(JournalMapping.ToDto(
                    entry, true, entry.CreatedByUserId == command.UserId));
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return JournalResult<JournalEntryDto>.Failure(JournalErrors.InvalidContent(exception.Message));
            }
        }
        finally
        {
            metrics.OperationCompleted("update", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class DeleteJournalEntryHandler(
    ICampaignAccessReader campaignAccess,
    IJournalEntryRepository entries,
    IJournalMetrics metrics)
{
    public async Task<JournalResult<bool>> HandleAsync(
        DeleteJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await JournalAuthorization.AuthorizePlayerAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access is not null)
            {
                outcome = JournalErrors.Outcome(access);
                return JournalResult<bool>.Failure(access);
            }

            var entry = await entries.FindForUpdateAsync(
                command.CampaignId, command.EntryId, cancellationToken);
            if (entry is null)
            {
                outcome = "not_found";
                return JournalResult<bool>.Failure(JournalErrors.EntryNotFound());
            }

            if (entry.CreatedByUserId != command.UserId)
            {
                outcome = "forbidden";
                return JournalResult<bool>.Failure(JournalErrors.Forbidden());
            }

            entries.Delete(entry);
            await entries.SaveChangesAsync(cancellationToken);
            outcome = "success";
            return JournalResult<bool>.Success(true);
        }
        finally
        {
            metrics.OperationCompleted("delete", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal static class JournalAuthorization
{
    public static async Task<(CampaignRole? Role, JournalError? Error)> AuthorizeAsync(
        ICampaignAccessReader campaignAccess,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await campaignAccess.GetAccessAsync(campaignId, userId, cancellationToken);
        if (!access.Exists)
        {
            return (null, JournalErrors.CampaignNotFound());
        }

        return access.Role is null
            ? (null, JournalErrors.Forbidden())
            : (access.Role, null);
    }

    public static async Task<JournalError?> AuthorizePlayerAsync(
        ICampaignAccessReader campaignAccess,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(campaignAccess, campaignId, userId, cancellationToken);
        return access.Error ?? (access.Role == CampaignRole.Player ? null : JournalErrors.Forbidden());
    }
}

internal static class JournalMapping
{
    public static JournalEntryDto ToDto(JournalEntry entry, bool canEdit, bool canDelete) => new(
        entry.Id,
        entry.CampaignId,
        entry.AuthorCharacterId,
        entry.AuthorCharacterName,
        entry.Content,
        entry.CreatedAt,
        entry.UpdatedAt,
        canEdit,
        canDelete);
}

internal static class JournalErrors
{
    public static JournalError InvalidContent(string description) => new(
        JournalErrorType.Validation,
        "journal.content_invalid",
        description,
        new Dictionary<string, string[]> { ["content"] = [description] });

    public static JournalError InvalidLimit() => new(
        JournalErrorType.Validation,
        "journal.limit_invalid",
        "El límite debe estar entre 1 y 50.",
        new Dictionary<string, string[]> { ["limit"] = ["El límite debe estar entre 1 y 50."] });

    public static JournalError InvalidCursor() => new(
        JournalErrorType.Validation,
        "journal.cursor_invalid",
        "El cursor de paginación no es válido.",
        new Dictionary<string, string[]> { ["cursor"] = ["El cursor de paginación no es válido."] });

    public static JournalError Forbidden() => new(
        JournalErrorType.Forbidden, "journal.forbidden", "No tienes permiso para realizar esta operación.");

    public static JournalError CampaignNotFound() => new(
        JournalErrorType.NotFound, "journal.campaign_not_found", "La campaña no existe.");

    public static JournalError EntryNotFound() => new(
        JournalErrorType.NotFound, "journal.entry_not_found", "La entrada no existe.");

    public static JournalError ActiveCharacterRequired() => new(
        JournalErrorType.Conflict,
        "journal.active_character_required",
        "Necesitas un personaje activo para introducir una entrada.");

    public static string Outcome(JournalError error) => error.Type switch
    {
        JournalErrorType.Validation => "validation",
        JournalErrorType.Forbidden => "forbidden",
        JournalErrorType.NotFound => "not_found",
        JournalErrorType.Conflict => "conflict",
        _ => "failure",
    };
}
