using ZohoWhatsAppAI.Domain.Enums;

namespace ZohoWhatsAppAI.Application.DTOs;

public class GenerateWhatsAppMessageRequest
{
    public string? AdditionalContext { get; set; }
    public WhatsAppTemplateType TemplateType { get; set; } = WhatsAppTemplateType.Qualification;
    public string? CustomInstructions { get; set; }
    public bool UseTemplateBuilder { get; set; }
}

public class GenerateWhatsAppMessageResponse
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public WhatsAppTemplateType TemplateType { get; set; }
    public WhatsAppTemplateDto? Template { get; set; }
    public string Model { get; set; } = string.Empty;
}

public class BuildWhatsAppTemplateRequest
{
    public WhatsAppTemplateType TemplateType { get; set; } = WhatsAppTemplateType.Qualification;
    public string? TemplateName { get; set; }
    public string? LanguageCode { get; set; } = "en";
    public string? Purpose { get; set; }
    public IReadOnlyDictionary<string, string>? Placeholders { get; set; }
}

public class WhatsAppTemplateDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en";
    public string Category { get; set; } = "MARKETING";
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<string> Placeholders { get; set; } = [];
    public string ComplianceNotes { get; set; } = string.Empty;
}

public class BuildWhatsAppTemplateResponse
{
    public WhatsAppTemplateDto Template { get; set; } = new();
    public string Model { get; set; } = string.Empty;
}

public class ConversationSummaryRequest
{
    public string ConversationTranscript { get; set; } = string.Empty;
    public string? AdditionalContext { get; set; }
}

public class ConversationSummaryResponse
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> KeyPoints { get; set; } = [];
    public string Sentiment { get; set; } = string.Empty;
    public string RecommendedNextStep { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
