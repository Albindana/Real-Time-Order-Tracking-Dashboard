using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealTimeDashboard.Application.Common;

/// <summary>
/// Shared JSON settings used for Redis payloads and SignalR so enums serialize as strings
/// (e.g. "Pending") and property names are camelCase to match the TypeScript client.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
