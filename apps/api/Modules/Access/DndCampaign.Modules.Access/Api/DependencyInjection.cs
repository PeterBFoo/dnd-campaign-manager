using DndCampaign.Modules.Access.Application.Bootstrap;
using DndCampaign.Modules.Access.Application.Identity;
using DndCampaign.Modules.Access.Application.Invitations;
using Microsoft.Extensions.DependencyInjection;

namespace DndCampaign.Modules.Access.Api;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAccessApi(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                if (manager.FeatureProviders.All(provider => provider is not InternalControllerFeatureProvider))
                {
                    manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
                }
            });
        services.AddScoped<GetBootstrapStatusHandler>();
        services.AddScoped<CompleteBootstrapHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<PreviewInvitationHandler>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<InvitationCommandHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        return services;
    }
}
