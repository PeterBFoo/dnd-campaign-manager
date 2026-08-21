using DndCampaign.Modules.Access.Application.Ports.Security;
using DndCampaign.Modules.Access.Domain.Accounts;
using Microsoft.AspNetCore.Identity;
using ApplicationPasswordVerificationResult = DndCampaign.Modules.Access.Application.Ports.Security.PasswordVerificationResult;
using AspNetPasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace DndCampaign.Modules.Access.Infrastructure.Security;

internal sealed class AspNetPasswordService(IPasswordHasher<UserAccount> passwordHasher) : IPasswordService
{
    public string Hash(UserAccount user, string password) => passwordHasher.HashPassword(user, password);

    public ApplicationPasswordVerificationResult Verify(
        UserAccount user,
        string passwordHash,
        string password) =>
        passwordHasher.VerifyHashedPassword(user, passwordHash, password) switch
        {
            AspNetPasswordVerificationResult.Failed => ApplicationPasswordVerificationResult.Failed,
            AspNetPasswordVerificationResult.SuccessRehashNeeded =>
                ApplicationPasswordVerificationResult.SuccessRehashNeeded,
            _ => ApplicationPasswordVerificationResult.Success,
        };
}
