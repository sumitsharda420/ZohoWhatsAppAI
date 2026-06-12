using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZohoWhatsAppAI.Application.Configuration;
using ZohoWhatsAppAI.Application.Interfaces;
using ZohoWhatsAppAI.Infrastructure.Configuration;
using ZohoWhatsAppAI.Infrastructure.External.OpenAI;
using ZohoWhatsAppAI.Infrastructure.External.Zoho;

namespace ZohoWhatsAppAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ZohoSettings>(configuration.GetSection(ZohoSettings.SectionName));
        services.Configure<OpenAISettings>(configuration.GetSection(OpenAISettings.SectionName));
        services.Configure<WhatsAppSettings>(configuration.GetSection(WhatsAppSettings.SectionName));

        services.AddHttpClient("ZohoOAuth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("ZohoCrm", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient("OpenAI", (serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<OpenAISettings>>().Value;
            client.BaseAddress = new Uri($"{settings.ApiBaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(120);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<ZohoTokenProvider>();
        services.AddScoped<IZohoCrmService, ZohoCrmService>();
        services.AddScoped<IOpenAIAnalysisService, OpenAIAnalysisService>();

        return services;
    }
}
