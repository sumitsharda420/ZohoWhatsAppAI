namespace ZohoWhatsAppAI.Domain.Entities;

public class LeadAnalysis
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadSummary { get; set; } = string.Empty;
    public int LeadScore { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
