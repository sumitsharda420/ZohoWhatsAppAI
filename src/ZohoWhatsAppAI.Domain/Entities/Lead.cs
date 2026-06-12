namespace ZohoWhatsAppAI.Domain.Entities;

public class Lead
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LeadSource { get; set; } = string.Empty;
    public string LeadStatus { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> AdditionalFields { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
