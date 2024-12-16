namespace Publisher;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class MqttMessagePayloadSerializer
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions;
    private static readonly ConcurrentDictionary<Type, JsonSerializerOptions> SerializerOptions;

    static MqttMessagePayloadSerializer()
    {
        DefaultJsonSerializerOptions = new JsonSerializerOptions();
        var converter = new JsonStringEnumConverter();
        DefaultJsonSerializerOptions.Converters.Add(converter);

        SerializerOptions = new ConcurrentDictionary<Type, JsonSerializerOptions>();
    }
    
    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, options: GetJsonSerializerOptionsOrDefault<T>());
    }

    private static JsonSerializerOptions GetJsonSerializerOptionsOrDefault<T>()
        => SerializerOptions.GetValueOrDefault(typeof(T), DefaultJsonSerializerOptions);
}