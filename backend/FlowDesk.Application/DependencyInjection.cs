using FlowDesk.Application.Documents;
using FlowDesk.Application.Expedients;
using FlowDesk.Application.Identity;
using FlowDesk.Application.Notifications;
using FlowDesk.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ExpedientService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<DocumentProcessingService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AuditQueryService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<BusinessEventReactions>();
        return services;
    }
}
