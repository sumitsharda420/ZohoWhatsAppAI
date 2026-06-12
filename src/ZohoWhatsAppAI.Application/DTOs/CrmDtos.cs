namespace ZohoWhatsAppAI.Application.DTOs;

public class SaveSummaryRequest
{
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public int? LeadScore { get; set; }
    public string? RecommendedAction { get; set; }
    public string? ConversationSummary { get; set; }
    public bool IncludeAnalysis { get; set; }
    public string? AdditionalContext { get; set; }
    public string? ConversationTranscript { get; set; }
}

public class SaveSummaryResponse
{
    public string LeadId { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public string NoteTitle { get; set; } = string.Empty;
    public string SavedContent { get; set; } = string.Empty;
    public bool SavedToZoho { get; set; }
}
