using System.Text.Json;

namespace ZohoWhatsAppAI.Infrastructure.External.Zoho;

internal static class ZohoJsonHelper
{
    public static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ConvertJsonValue(property.Value);
        }

        return result;
    }

    private static object? ConvertJsonValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonElementToDictionary(value),
            JsonValueKind.Array => value.EnumerateArray()
                .Select(ConvertJsonValue)
                .ToList(),
            _ => value.ToString()
        };
}

internal sealed class ZohoApiResponse<T>
{
    public List<T>? Data { get; set; }
}
