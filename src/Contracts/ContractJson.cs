using System.Text.Json;
using System.Text.Json.Serialization;

namespace Contracts;

/// <summary>Serializer settings for everything that crosses a service boundary (WebSocket messages, blobs).</summary>
public static class ContractJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
