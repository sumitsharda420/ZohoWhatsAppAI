using ZohoWhatsAppAI.Application.DTOs;
using ZohoWhatsAppAI.Domain.Entities;

namespace ZohoWhatsAppAI.Application.Interfaces;

public interface IZohoCrmService
{
    Task<Lead?> GetLeadByIdAsync(string leadId, CancellationToken cancellationToken = default);
    Task<string> CreateLeadNoteAsync(
        string leadId,
        string title,
        string content,
        CancellationToken cancellationToken = default);
}

public interface IOpenAIAnalysisService
{
    Task<LeadAnalysis> AnalyzeLeadAsync(
        Lead lead,
        string? additionalContext,
        CancellationToken cancellationToken = default);

    Task<string> GenerateWhatsAppMessageAsync(
        Lead lead,
        string? additionalContext,
        string templateGuidance,
        CancellationToken cancellationToken = default);

    Task<WhatsAppTemplateDto> BuildWhatsAppTemplateAsync(
        BuildWhatsAppTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<ConversationSummaryResponse> GenerateConversationSummaryAsync(
        Lead lead,
        ConversationSummaryRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILeadAnalysisService
{
    Task<AnalyzeLeadResponse> AnalyzeLeadAsync(
        string leadId,
        AnalyzeLeadRequest? request,
        CancellationToken cancellationToken = default);
}

public interface IWhatsAppService
{
    Task<GenerateWhatsAppMessageResponse> GenerateMessageAsync(
        string leadId,
        GenerateWhatsAppMessageRequest? request,
        CancellationToken cancellationToken = default);

    Task<BuildWhatsAppTemplateResponse> BuildTemplateAsync(
        BuildWhatsAppTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<ConversationSummaryResponse> GenerateConversationSummaryAsync(
        string leadId,
        ConversationSummaryRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICrmSummaryService
{
    Task<SaveSummaryResponse> SaveSummaryAsync(
        string leadId,
        SaveSummaryRequest? request,
        CancellationToken cancellationToken = default);
}
