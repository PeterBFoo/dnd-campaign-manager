using System.Diagnostics;
using DndCampaign.Modules.Access.Application.Abstractions.Messaging;
using DndCampaign.Modules.Access.Application.Abstractions.Results;
using DndCampaign.Modules.Access.Application.Identity;
using DndCampaign.Modules.Access.Application.Ports.Observability;
using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Contracts.CampaignAccess;

namespace DndCampaign.Modules.Access.Application.Users;

internal sealed record SearchEligibleUsersQuery(
    Guid CampaignId,
    string? Query,
    string? Cursor,
    int? Limit,
    AccessActor Actor);

internal sealed record EligibleUserDto(Guid UserId, string DisplayName, string MaskedEmail);

internal sealed record EligibleUsersPageDto(
    IReadOnlyList<EligibleUserDto> Items,
    string? NextCursor);

internal sealed class SearchEligibleUsersHandler(
    ICampaignInvitationContext campaigns,
    IEligibleUserReadStore users,
    IAccessMetrics metrics,
    TimeProvider timeProvider)
    : IQueryHandler<SearchEligibleUsersQuery, Result<EligibleUsersPageDto>>
{
    public async Task<Result<EligibleUsersPageDto>> HandleAsync(
        SearchEligibleUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            if (!query.Actor.UserId.HasValue)
            {
                outcome = "forbidden";
                return Forbidden();
            }

            var access = await campaigns.GetAccessAsync(
                query.CampaignId,
                query.Actor.UserId.Value,
                cancellationToken);
            if (!access.Exists || !access.IsDm)
            {
                outcome = "forbidden";
                return Forbidden();
            }

            var search = string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim();
            if (search is { Length: < 2 })
            {
                outcome = "validation";
                return Result<EligibleUsersPageDto>.Failure(new ApplicationError(
                    "users.search_too_short",
                    ApplicationErrorType.Validation,
                    "La búsqueda debe contener al menos dos caracteres.",
                    new Dictionary<string, string[]> { ["query"] = ["Introduce al menos dos caracteres."] }));
            }

            if (!TryParseCursor(query.Cursor, out var offset))
            {
                outcome = "validation";
                return Result<EligibleUsersPageDto>.Failure(new ApplicationError(
                    "users.invalid_cursor",
                    ApplicationErrorType.Validation,
                    "El cursor de paginación no es válido."));
            }

            var limit = Math.Clamp(query.Limit ?? 20, 1, 50);
            var page = await users.SearchAsync(
                query.CampaignId,
                query.Actor.UserId.Value,
                search,
                offset,
                limit,
                timeProvider.GetUtcNow(),
                cancellationToken);
            outcome = "success";
            return Result<EligibleUsersPageDto>.Success(new EligibleUsersPageDto(
                page.Items.Select(item => new EligibleUserDto(
                    item.UserId,
                    item.DisplayName,
                    MaskEmail(item.Email))).ToArray(),
                page.HasMore ? Convert.ToBase64String(BitConverter.GetBytes(offset + limit)) : null));
        }
        finally
        {
            metrics.EligibleUsersSearched(outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private static bool TryParseCursor(string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            if (bytes.Length != sizeof(int))
            {
                return false;
            }

            offset = BitConverter.ToInt32(bytes);
            return offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return "***";
        }

        var local = email[..separator];
        var visible = local.Length == 1 ? local : local[..Math.Min(2, local.Length)];
        return $"{visible}***{email[separator..]}";
    }

    private static Result<EligibleUsersPageDto> Forbidden() =>
        Result<EligibleUsersPageDto>.Failure(new ApplicationError(
            "access.forbidden",
            ApplicationErrorType.Forbidden,
            "No tienes permisos para consultar usuarios elegibles."));
}
