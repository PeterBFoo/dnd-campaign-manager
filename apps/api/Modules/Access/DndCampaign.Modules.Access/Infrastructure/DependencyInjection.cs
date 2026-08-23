using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DndCampaign.Modules.Access.Application.Ports.Email;
using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Application.Ports.Observability;
using DndCampaign.Modules.Access.Application.Ports.Security;
using DndCampaign.Modules.Access.Domain.Accounts;
using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Access.Infrastructure.Authentication;
using DndCampaign.Modules.Access.Infrastructure.Email;
using DndCampaign.Modules.Access.Infrastructure.Persistence;
using DndCampaign.Modules.Access.Infrastructure.Observability;
using DndCampaign.Modules.Access.Infrastructure.Security;
using DndCampaign.Modules.Access.Infrastructure.Outbox;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAccessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var securityOptions = AccessSecurityOptions.FromConfiguration(configuration, environment);
        services.AddSingleton(securityOptions);
        services.AddSingleton<InvitationTokenProtector>();
        services.AddSingleton<IInvitationTokenProtector>(provider =>
            provider.GetRequiredService<InvitationTokenProtector>());
        services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddSingleton<IPasswordService, AspNetPasswordService>();
        services.AddSingleton<IBootstrapTokenVerifier, BootstrapTokenVerifier>();
        services.AddSingleton<IAccessMetrics, AccessMetrics>();

        services.AddDbContext<AccessDbContext>(options => options.UseNpgsql(
            connectionString));
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IEligibleUserReadStore>(provider =>
            provider.GetRequiredService<IUserAccountRepository>() as IEligibleUserReadStore
            ?? throw new InvalidOperationException("User account repository must implement eligible user reads."));
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IInvitationReadStore>(provider =>
            provider.GetRequiredService<IInvitationRepository>() as IInvitationReadStore
            ?? throw new InvalidOperationException("Invitation repository must implement the read store."));
        services.AddScoped<IInvitationOutboxRepository, InvitationOutboxRepository>();
        services.AddScoped<ICampaignAccessRepository, CampaignAccessRepository>();
        services.AddScoped<IPlayerCampaignAccessReader>(provider =>
            provider.GetRequiredService<ICampaignAccessRepository>() as IPlayerCampaignAccessReader
            ?? throw new InvalidOperationException("Campaign access repository must implement player access reads."));
        services.AddScoped<ICampaignPlayerReader>(provider =>
            provider.GetRequiredService<ICampaignAccessRepository>() as ICampaignPlayerReader
            ?? throw new InvalidOperationException("Campaign access repository must implement campaign player reads."));
        services.AddScoped<IAccessUnitOfWork, AccessUnitOfWork>();
        services.AddSingleton<InvitationEmailComposer>();
        if (configuration.GetValue("Email:OutboxWorkerEnabled", false))
        {
            services.AddHostedService<InvitationOutboxWorker>();
        }

        services
            .AddAuthentication(SessionAuthenticationHandler.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                SessionAuthenticationHandler.AuthenticationScheme,
                _ => { });

        services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
        services.AddHttpClient<ITransactionalEmailSender, BrevoEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }
}
