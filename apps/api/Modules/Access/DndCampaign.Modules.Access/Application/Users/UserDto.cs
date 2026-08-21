using DndCampaign.Modules.Access.Domain.Accounts;

namespace DndCampaign.Modules.Access.Application.Users;

internal sealed record UserDto(Guid Id, string Email, string DisplayName, bool IsPlatformAdmin)
{
    public static UserDto FromDomain(UserAccount user) =>
        new(user.Id, user.Email, user.DisplayName, user.IsPlatformAdmin);
}
