namespace ZohoWhatsAppAI.Infrastructure.Configuration;

public class ZohoSettings
{
    public const string SectionName = "Zoho";

    public string ApiBaseUrl { get; set; } = "https://www.zohoapis.com";
    public string AccountsUrl { get; set; } = "https://accounts.zoho.com/oauth/v2/token";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public bool UseMockApi { get; set; } = true;
}
