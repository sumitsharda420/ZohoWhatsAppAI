using Microsoft.Extensions.DependencyInjection;
using ZohoWhatsAppAI.Application.Interfaces;
using ZohoWhatsAppAI.Application.Services;

namespace ZohoWhatsAppAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ILeadAnalysisService, LeadAnalysisService>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<ICrmSummaryService, CrmSummaryService>();
        return services;
    }
}
