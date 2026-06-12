using Microsoft.Extensions.Logging;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Application.DTOs;
using ZohoWhatsAppAI.Application.Interfaces;

namespace ZohoWhatsAppAI.Application.Services;

public class LeadAnalysisService : ILeadAnalysisService
{
    private readonly IZohoCrmService _zohoCrmService;
    private readonly IOpenAIAnalysisService _openAiService;
    private readonly ILogger<LeadAnalysisService> _logger;

    public LeadAnalysisService(
        IZohoCrmService zohoCrmService,
        IOpenAIAnalysisService openAiService,
        ILogger<LeadAnalysisService> logger)
    {
        _zohoCrmService = zohoCrmService;
        _openAiService = openAiService;
        _logger = logger;
    }

    public async Task<AnalyzeLeadResponse> AnalyzeLeadAsync(
        string leadId,
        AnalyzeLeadRequest? request,
        CancellationToken cancellationToken = default)
    {
        var lead = await GetLeadOrThrowAsync(leadId, cancellationToken);

        _logger.LogInformation("Analyzing lead {LeadId} with OpenAI.", leadId);

        var analysis = await _openAiService.AnalyzeLeadAsync(
            lead,
            request?.AdditionalContext,
            cancellationToken);

        return new AnalyzeLeadResponse
        {
            LeadId = lead.Id,
            LeadName = lead.FullName,
            Company = lead.Company,
            Email = lead.Email,
            Phone = lead.Phone,
            LeadSummary = analysis.LeadSummary,
            LeadScore = analysis.LeadScore,
            RecommendedAction = analysis.RecommendedAction,
            Model = analysis.Model
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
