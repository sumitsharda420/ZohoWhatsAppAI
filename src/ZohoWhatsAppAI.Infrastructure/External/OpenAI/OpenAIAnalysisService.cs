using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Application.DTOs;
using ZohoWhatsAppAI.Application.Interfaces;
using ZohoWhatsAppAI.Domain.Entities;
using ZohoWhatsAppAI.Domain.Enums;
using ZohoWhatsAppAI.Application.Configuration;
using ZohoWhatsAppAI.Infrastructure.Configuration;

namespace ZohoWhatsAppAI.Infrastructure.External.OpenAI;

public class OpenAIAnalysisService : IOpenAIAnalysisService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAISettings _settings;
    private readonly WhatsAppSettings _whatsAppSettings;
    private readonly ILogger<OpenAIAnalysisService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAIAnalysisService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAISettings> settings,
        IOptions<WhatsAppSettings> whatsAppSettings,
        ILogger<OpenAIAnalysisService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _whatsAppSettings = whatsAppSettings.Value;
        _logger = logger;
    }

    public async Task<LeadAnalysis> AnalyzeLeadAsync(
        Lead lead,
        string? additionalContext,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        const string systemPrompt =
            """
            You are a senior B2B sales analyst specializing in lead qualification.
            Respond ONLY with valid JSON using this schema:
            {
              "leadSummary": "string",
              "leadScore": 0,
              "recommendedAction": "string"
            }
            leadScore must be an integer from 1 to 100.
            """;

        var userPrompt = BuildLeadPrompt(
            lead,
            additionalContext,
            """
            Analyze this CRM lead and provide:
            1. Lead Summary
            2. Lead Score (1-100)
            3. Recommended Action for sales follow-up
            """);

        var result = await SendChatCompletionAsync<OpenAILeadAnalysisResult>(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            cancellationToken);

        return new LeadAnalysis
        {
            LeadId = lead.Id,
            LeadSummary = result.LeadSummary ?? string.Empty,
            LeadScore = Math.Clamp(result.LeadScore, 1, 100),
            RecommendedAction = result.RecommendedAction ?? string.Empty,
            Model = _settings.Model
        };
    }

    public async Task<string> GenerateWhatsAppMessageAsync(
        Lead lead,
        string? additionalContext,
        string templateGuidance,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        const string systemPrompt =
            """
            You are a WhatsApp sales engagement assistant.
            Write concise, conversational messages suitable for WhatsApp Business.
            Respond ONLY with valid JSON: { "message": "string" }
            Keep messages under 500 characters unless instructed otherwise.
            """;

        var userPrompt = BuildLeadPrompt(
            lead,
            additionalContext,
            $"""
            Generate a WhatsApp message for this lead.
            Template guidance:
            {templateGuidance}
            Max length: {_whatsAppSettings.MaxMessageLength} characters.
            """);

        var result = await SendChatCompletionAsync<OpenAIMessageResult>(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            cancellationToken);

        return result.Message ?? string.Empty;
    }

    public async Task<WhatsAppTemplateDto> BuildWhatsAppTemplateAsync(
        BuildWhatsAppTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        const string systemPrompt =
            """
            You design WhatsApp Business API message templates for sales teams.
            Respond ONLY with valid JSON:
            {
              "templateName": "string",
              "languageCode": "en",
              "category": "MARKETING",
              "body": "string with {{placeholders}}",
              "placeholders": ["string"],
              "complianceNotes": "string"
            }
            Use WhatsApp-style placeholders like {{1}}, {{customer_name}}, etc.
            """;

        var placeholders = request.Placeholders is null
            ? "None provided"
            : string.Join(", ", request.Placeholders.Select(pair => $"{pair.Key}={pair.Value}"));

        var userPrompt =
            $"""
            Build a WhatsApp template.
            Template type: {request.TemplateType}
            Template name hint: {request.TemplateName ?? "auto-generate"}
            Language: {request.LanguageCode ?? _whatsAppSettings.DefaultLanguageCode}
            Purpose: {request.Purpose ?? "Lead qualification outreach"}
            Known placeholder values: {placeholders}
            """;

        var result = await SendChatCompletionAsync<OpenAITemplateResult>(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            cancellationToken);

        return new WhatsAppTemplateDto
        {
            TemplateName = result.TemplateName ?? $"lead_{request.TemplateType.ToString().ToLowerInvariant()}",
            LanguageCode = result.LanguageCode ?? request.LanguageCode ?? _whatsAppSettings.DefaultLanguageCode,
            Category = result.Category ?? "MARKETING",
            Body = result.Body ?? string.Empty,
            Placeholders = result.Placeholders ?? [],
            ComplianceNotes = result.ComplianceNotes ??
                "Submit this template to WhatsApp Business Manager for approval before sending."
        };
    }

    public async Task<ConversationSummaryResponse> GenerateConversationSummaryAsync(
        Lead lead,
        ConversationSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        const string systemPrompt =
            """
            You summarize WhatsApp sales conversations for CRM notes.
            Respond ONLY with valid JSON:
            {
              "summary": "string",
              "keyPoints": ["string"],
              "sentiment": "positive|neutral|negative",
              "recommendedNextStep": "string"
            }
            """;

        var userPrompt =
            $"""
            Summarize this WhatsApp conversation for lead {lead.FullName} at {lead.Company}.

            Lead context:
            {FormatLead(lead)}

            Conversation transcript:
            {request.ConversationTranscript}

            Additional context:
            {request.AdditionalContext ?? "None"}
            """;

        var result = await SendChatCompletionAsync<OpenAIConversationSummaryResult>(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            cancellationToken);

        return new ConversationSummaryResponse
        {
            LeadId = lead.Id,
            LeadName = lead.FullName,
            Summary = result.Summary ?? string.Empty,
            KeyPoints = result.KeyPoints ?? [],
            Sentiment = result.Sentiment ?? "neutral",
            RecommendedNextStep = result.RecommendedNextStep ?? string.Empty,
            Model = _settings.Model
        };
    }

    private async Task<T> SendChatCompletionAsync<T>(
        string systemPrompt,
        string userPrompt,
        bool jsonMode,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("OpenAI");

        var requestBody = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = jsonMode ? new { type = "json_object" } : null,
            temperature = 0.4
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenAI API call failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                responseBody);

            throw new ExternalServiceException(
                $"OpenAI API request failed: {(int)response.StatusCode}. {responseBody}",
                (int)response.StatusCode);
        }

        var completion = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(responseBody, JsonOptions);
        var messageContent = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(messageContent))
        {
            throw new ExternalServiceException("OpenAI returned an empty completion.", (int)HttpStatusCode.BadGateway);
        }

        var result = JsonSerializer.Deserialize<T>(messageContent, JsonOptions);
        if (result is null)
        {
            throw new ExternalServiceException(
                $"Failed to parse OpenAI JSON response: {messageContent}",
                (int)HttpStatusCode.BadGateway);
        }

        return result;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new ConfigurationException(
                "OpenAI API key is not configured. Set OpenAI:ApiKey in appsettings.json or user secrets.");
        }
    }

    private static string BuildLeadPrompt(Lead lead, string? additionalContext, string task)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine(task);
        prompt.AppendLine();
        prompt.AppendLine("Lead data:");
        prompt.AppendLine(FormatLead(lead));

        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            prompt.AppendLine();
            prompt.AppendLine("Additional context:");
            prompt.AppendLine(additionalContext);
        }

        return prompt.ToString();
    }

    private static string FormatLead(Lead lead) =>
        $"""
        Lead ID: {lead.Id}
        Lead Name: {lead.FullName}
        Company: {lead.Company}
        Email: {lead.Email}
        Phone: {lead.Phone}
        Lead Source: {lead.LeadSource}
        Lead Status: {lead.LeadStatus}
        Description: {lead.Description}
        Notes: {lead.Notes}
        """;

    private sealed class OpenAIChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }
    }

    private sealed class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }

    private sealed class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class OpenAILeadAnalysisResult
    {
        [JsonPropertyName("leadSummary")]
        public string? LeadSummary { get; set; }

        [JsonPropertyName("leadScore")]
        public int LeadScore { get; set; }

        [JsonPropertyName("recommendedAction")]
        public string? RecommendedAction { get; set; }
    }

    private sealed class OpenAIMessageResult
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private sealed class OpenAITemplateResult
    {
        [JsonPropertyName("templateName")]
        public string? TemplateName { get; set; }

        [JsonPropertyName("languageCode")]
        public string? LanguageCode { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("placeholders")]
        public List<string>? Placeholders { get; set; }

        [JsonPropertyName("complianceNotes")]
        public string? ComplianceNotes { get; set; }
    }

    private sealed class OpenAIConversationSummaryResult
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("keyPoints")]
        public List<string>? KeyPoints { get; set; }

        [JsonPropertyName("sentiment")]
        public string? Sentiment { get; set; }

        [JsonPropertyName("recommendedNextStep")]
        public string? RecommendedNextStep { get; set; }
    }
}
