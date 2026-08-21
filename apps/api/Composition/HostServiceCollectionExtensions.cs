using DndCampaign.Api.Application;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Infrastructure.Email;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Composition;

public static class HostServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<CampaignDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<IInvitationStore, InvitationStore>();
        services.AddScoped<IInvitationOutboxStore, InvitationOutboxStore>();
        services.AddScoped<ITransactionalBoundary, SerializableTransactionalBoundary>();
        services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
        return services;
    }

    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IdentitySecurityOptions identitySecurity)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(identitySecurity);
        services.AddSingleton<InvitationTokenProtector>();
        services.AddSingleton<InvitationEmailComposer>();
        services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddScoped<InvitationIssuanceCore>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IInvitationAcceptanceService, InvitationAcceptanceService>();
        services.AddScoped<IPlatformInvitationService, PlatformInvitationService>();
        services.AddScoped<ICampaignInvitationService, CampaignInvitationService>();
        services.AddScoped<ProcessInvitationOutbox>();
        return services;
    }

    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
        services.AddHttpClient<ITransactionalEmailSender, BrevoEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }
}
