namespace ZohoWhatsAppAI.Application.DTOs;

public class AnalyzeLeadRequest
{
    public string? AdditionalContext { get; set; }
}

public class AnalyzeLeadResponse
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LeadSummary { get; set; } = string.Empty;
    public int LeadScore { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
