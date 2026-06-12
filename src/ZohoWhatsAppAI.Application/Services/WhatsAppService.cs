using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Application.DTOs;
using ZohoWhatsAppAI.Application.Interfaces;
using ZohoWhatsAppAI.Domain.Enums;
using ZohoWhatsAppAI.Application.Configuration;

namespace ZohoWhatsAppAI.Application.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly IZohoCrmService _zohoCrmService;
    private readonly IOpenAIAnalysisService _openAiService;
    private readonly OpenAISettings _openAiSettings;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        IZohoCrmService zohoCrmService,
        IOpenAIAnalysisService openAiService,
        IOptions<OpenAISettings> openAiSettings,
        ILogger<WhatsAppService> logger)
    {
        _zohoCrmService = zohoCrmService;
        _openAiService = openAiService;
        _openAiSettings = openAiSettings.Value;
        _logger = logger;
    }

    public async Task<GenerateWhatsAppMessageResponse> GenerateMessageAsync(
        string leadId,
        GenerateWhatsAppMessageRequest? request,
        CancellationToken cancellationToken = default)
    {
        var lead = await GetLeadOrThrowAsync(leadId, cancellationToken);
        request ??= new GenerateWhatsAppMessageRequest();

        WhatsAppTemplateDto? template = null;
        if (request.UseTemplateBuilder)
        {
            template = await _openAiService.BuildWhatsAppTemplateAsync(
                new BuildWhatsAppTemplateRequest
                {
                    TemplateType = request.TemplateType,
                    Purpose = request.CustomInstructions ?? $"WhatsApp outreach for {request.TemplateType}"
                },
                cancellationToken);
        }

        var templateGuidance = BuildTemplateGuidance(request.TemplateType, template);

        _logger.LogInformation(
            "Generating WhatsApp message for lead {LeadId} using template {TemplateType}.",
            leadId,
            request.TemplateType);

        var message = await _openAiService.GenerateWhatsAppMessageAsync(
            lead,
            request.AdditionalContext,
            templateGuidance,
            cancellationToken);

        return new GenerateWhatsAppMessageResponse
        {
            LeadId = lead.Id,
            LeadName = lead.FullName,
            Phone = lead.Phone,
            Message = message,
            TemplateType = request.TemplateType,
            Template = template,
            Model = _openAiSettings.Model
        };
    }

    public async Task<BuildWhatsAppTemplateResponse> BuildTemplateAsync(
        BuildWhatsAppTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building WhatsApp template of type {TemplateType}.", request.TemplateType);

        var template = await _openAiService.BuildWhatsAppTemplateAsync(request, cancellationToken);

        return new BuildWhatsAppTemplateResponse
        {
            Template = template,
            Model = _openAiSettings.Model
        };
    }

    public async Task<ConversationSummaryResponse> GenerateConversationSummaryAsync(
        string leadId,
        ConversationSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationTranscript))
        {
            throw new ArgumentException("Conversation transcript is required.", nameof(request));
        }

        var lead = await GetLeadOrThrowAsync(leadId, cancellationToken);

        _logger.LogInformation("Generating conversation summary for lead {LeadId}.", leadId);

        return await _openAiService.GenerateConversationSummaryAsync(lead, request, cancellationToken);
    }

    private static string BuildTemplateGuidance(
        WhatsAppTemplateType templateType,
        WhatsAppTemplateDto? template)
    {
        if (template is not null)
        {
            return $"""
                Use this approved WhatsApp template structure:
                Template Name: {template.TemplateName}
                Body: {template.Body}
                Placeholders: {string.Join(", ", template.Placeholders)}
                Compliance: {template.ComplianceNotes}
                """;
        }

        return templateType switch
        {
            WhatsAppTemplateType.Qualification =>
                "Write a short WhatsApp message to qualify the lead's budget, timeline, and decision authority.",
            WhatsAppTemplateType.FollowUp =>
                "Write a polite WhatsApp follow-up referencing prior interest and offering a clear next step.",
            WhatsAppTemplateType.AppointmentReminder =>
                "Write a concise appointment reminder with date/time placeholder and confirmation request.",
            WhatsAppTemplateType.ReEngagement =>
                "Write a re-engagement message for an inactive lead with a low-friction call to action.",
            _ => "Write a professional WhatsApp sales message under 500 characters with one clear CTA."
        };
    }

    private async Task<Domain.Entities.Lead> GetLeadOrThrowAsync(
        string leadId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            throw new ArgumentException("Lead ID cannot be empty.", nameof(leadId));
        }

        var lead = await _zohoCrmService.GetLeadByIdAsync(leadId, cancellationToken);
        if (lead is null)
        {
            throw new NotFoundException($"Lead '{leadId}' was not found in Zoho CRM.");
        }

        return lead;
    }
}
