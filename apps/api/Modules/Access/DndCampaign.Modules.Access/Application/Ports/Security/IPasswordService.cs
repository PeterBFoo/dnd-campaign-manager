using DndCampaign.Modules.Access.Domain.Accounts;

namespace DndCampaign.Modules.Access.Application.Ports.Security;

internal enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

internal interface IPasswordService
{
    string Hash(UserAccount user, string password);

    PasswordVerificationResult Verify(UserAccount user, string passwordHash, string password);
}
