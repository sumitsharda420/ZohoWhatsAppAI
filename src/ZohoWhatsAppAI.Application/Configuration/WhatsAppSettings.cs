namespace ZohoWhatsAppAI.Application.Configuration;

public class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    public int MaxMessageLength { get; set; } = 1024;
    public string DefaultLanguageCode { get; set; } = "en";
}
