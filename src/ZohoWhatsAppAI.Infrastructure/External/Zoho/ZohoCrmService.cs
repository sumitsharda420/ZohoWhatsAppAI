using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Application.Interfaces;
using ZohoWhatsAppAI.Domain.Entities;
using ZohoWhatsAppAI.Infrastructure.Configuration;
using ZohoWhatsAppAI.Infrastructure.External.Zoho;

namespace ZohoWhatsAppAI.Infrastructure.External.Zoho;

public class ZohoTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ZohoSettings _settings;
    private readonly ILogger<ZohoTokenProvider> _logger;
    private string? _accessToken;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ZohoTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ZohoSettings> settings,
        ILogger<ZohoTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_settings.UseMockApi)
        {
            return "mock-access-token";
        }

        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _expiresAtUtc)
        {
            return _accessToken;
        }

        if (string.IsNullOrWhiteSpace(_settings.RefreshToken))
        {
            throw new ConfigurationException(
                "Zoho refresh token is not configured. Set Zoho:RefreshToken or enable Zoho:UseMockApi.");
        }

        var client = _httpClientFactory.CreateClient("ZohoOAuth");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = _settings.RefreshToken,
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["grant_type"] = "refresh_token"
        });

        using var response = await client.PostAsync(_settings.AccountsUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException(
                $"Zoho token refresh failed: {(int)response.StatusCode}. {body}",
                (int)response.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new ExternalServiceException("Zoho token response did not include access_token.");

        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresElement)
            ? expiresElement.GetInt32()
            : 3600;
        _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);

        _logger.LogInformation("Refreshed Zoho access token.");
        return _accessToken;
    }

    public void InvalidateToken() => _expiresAtUtc = DateTime.MinValue;
}

public class ZohoCrmService : IZohoCrmService
{
    private const string LeadFields =
        "id,First_Name,Last_Name,Full_Name,Company,Email,Phone,Mobile,Description,Lead_Source,Lead_Status";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ZohoTokenProvider _tokenProvider;
    private readonly ZohoSettings _settings;
    private readonly ILogger<ZohoCrmService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ZohoCrmService(
        IHttpClientFactory httpClientFactory,
        ZohoTokenProvider tokenProvider,
        IOptions<ZohoSettings> settings,
        ILogger<ZohoCrmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Lead?> GetLeadByIdAsync(string leadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            throw new ArgumentException("Lead ID cannot be empty.", nameof(leadId));
        }

        if (_settings.UseMockApi)
        {
            return CreateMockLead(leadId);
        }

        var url = AppendQueryString(
            $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/Leads/{leadId}",
            new Dictionary<string, string?> { ["fields"] = LeadFields });

        var leadBody = await SendAuthenticatedGetAsync(url, cancellationToken, allowNotFound: true);
        if (leadBody is null)
        {
            return null;
        }

        var leadRecord = DeserializeFirstRecord(leadBody);
        if (leadRecord is null)
        {
            return null;
        }

        var notes = await GetLeadNotesTextAsync(leadId, cancellationToken);
        return MapLead(leadId, leadRecord.Value, notes);
    }

    public async Task<string> CreateLeadNoteAsync(
        string leadId,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (_settings.UseMockApi)
        {
            var mockNoteId = $"NOTE-{Guid.NewGuid():N}"[..18];
            _logger.LogInformation(
                "Mock Zoho note created for lead {LeadId}: {NoteId}",
                leadId,
                mockNoteId);
            return mockNoteId;
        }

        var url = $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/Leads/{leadId}/Notes";
        var payload = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    Note_Title = title,
                    Note_Content = content
                }
            }
        });

        var responseBody = await SendAuthenticatedPostAsync(url, payload, cancellationToken);
        return ExtractRecordId(responseBody) ?? $"NOTE-{Guid.NewGuid():N}"[..18];
    }

    private async Task<string> GetLeadNotesTextAsync(string leadId, CancellationToken cancellationToken)
    {
        var url = AppendQueryString(
            $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/Leads/{leadId}/Notes",
            new Dictionary<string, string?>
            {
                ["fields"] = "Note_Content,Note_Title,Created_Time",
                ["per_page"] = "10"
            });

        var body = await SendAuthenticatedGetAsync(url, cancellationToken, allowNotFound: true);
        if (body is null)
        {
            return string.Empty;
        }

        var response = JsonSerializer.Deserialize<ZohoApiResponse<JsonElement>>(body, JsonOptions);
        if (response?.Data is null || response.Data.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var note in response.Data)
        {
            if (note.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fields = ZohoJsonHelper.JsonElementToDictionary(note);
            var title = fields.GetValueOrDefault("Note_Title")?.ToString();
            var content = fields.GetValueOrDefault("Note_Content")?.ToString();
            var created = fields.GetValueOrDefault("Created_Time")?.ToString();

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            lines.Add(string.IsNullOrWhiteSpace(title)
                ? $"- ({created}): {content}"
                : $"- {title} ({created}): {content}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<string?> SendAuthenticatedGetAsync(
        string url,
        CancellationToken cancellationToken,
        bool allowNotFound = false,
        bool isRetry = false)
    {
        _logger.LogInformation("Calling Zoho CRM GET {Url}", url);

        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("ZohoCrm");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
        {
            _tokenProvider.InvalidateToken();
            return await SendAuthenticatedGetAsync(url, cancellationToken, allowNotFound, isRetry: true);
        }

        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException(
                $"Zoho CRM request failed: {(int)response.StatusCode}. {body}",
                (int)response.StatusCode);
        }

        return body;
    }

    private async Task<string> SendAuthenticatedPostAsync(
        string url,
        string payload,
        CancellationToken cancellationToken,
        bool isRetry = false)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("ZohoCrm");

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
        {
            _tokenProvider.InvalidateToken();
            return await SendAuthenticatedPostAsync(url, payload, cancellationToken, isRetry: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException(
                $"Zoho CRM request failed: {(int)response.StatusCode}. {body}",
                (int)response.StatusCode);
        }

        return body;
    }

    private static JsonElement? DeserializeFirstRecord(string body)
    {
        var response = JsonSerializer.Deserialize<ZohoApiResponse<JsonElement>>(body, JsonOptions);
        var record = response?.Data?.FirstOrDefault();
        return record?.ValueKind == JsonValueKind.Object ? record : null;
    }

    private static Lead MapLead(string leadId, JsonElement record, string notes)
    {
        var fields = ZohoJsonHelper.JsonElementToDictionary(record);

        string Get(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (fields.TryGetValue(key, out var value) && value is not null)
                {
                    var text = value.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

        var fullName = Get("Full_Name");
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = string.Join(' ', new[] { Get("First_Name"), Get("Last_Name") }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        var additional = fields
            .Where(pair => !IsCoreField(pair.Key))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        return new Lead
        {
            Id = Get("id", "Id") is { Length: > 0 } id ? id : leadId,
            FullName = string.IsNullOrWhiteSpace(fullName) ? "Unknown" : fullName,
            Company = Get("Company"),
            Email = Get("Email"),
            Phone = Get("Phone", "Mobile"),
            Description = Get("Description"),
            LeadSource = Get("Lead_Source"),
            LeadStatus = Get("Lead_Status"),
            Notes = notes,
            AdditionalFields = additional
        };
    }

    private static bool IsCoreField(string fieldName) =>
        fieldName is "id" or "First_Name" or "Last_Name" or "Full_Name" or "Company"
            or "Email" or "Phone" or "Mobile" or "Description" or "Lead_Source" or "Lead_Status";

    private static string? ExtractRecordId(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
        {
            return null;
        }

        var first = data[0];
        if (first.TryGetProperty("details", out var details) &&
            details.TryGetProperty("id", out var idElement))
        {
            return idElement.GetString();
        }

        if (first.TryGetProperty("id", out var directId))
        {
            return directId.GetString();
        }

        return null;
    }

    private static string AppendQueryString(string baseUrl, IDictionary<string, string?> query)
    {
        var pairs = query
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}");

        var queryString = string.Join('&', pairs);
        return string.IsNullOrEmpty(queryString) ? baseUrl : $"{baseUrl}?{queryString}";
    }

    private static Lead CreateMockLead(string leadId) =>
        new()
        {
            Id = leadId,
            FullName = "Jane Doe",
            Company = "Acme Industries",
            Email = "jane.doe@acme.example",
            Phone = "+1-555-0100",
            Description = "Interested in enterprise CRM integration and WhatsApp automation.",
            LeadSource = "Website",
            LeadStatus = "Contacted",
            Notes = "- Initial inquiry about pricing and onboarding timeline.",
            AdditionalFields = new Dictionary<string, string>
            {
                ["Industry"] = "Manufacturing",
                ["Annual_Revenue"] = "5000000"
            }
        };
}
