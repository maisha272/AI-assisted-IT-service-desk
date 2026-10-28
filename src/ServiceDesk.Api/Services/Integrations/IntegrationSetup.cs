namespace ServiceDesk.Api.Services.Integrations;

public static class IntegrationSetup
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<IntegrationOptions>(config.GetSection(IntegrationOptions.SectionName));
        services.AddHttpClient<ITeamsNotifier, TeamsWebhookNotifier>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<InboundEmailService>();
        services.AddHostedService<NotificationWorker>();
        return services;
    }
}
