using System.Text.Json.Serialization;

namespace HxScaffoldSample;

/// <summary>A request to greet someone. A serializable DTO that lives in the library, not the CLI.</summary>
[SerializableContract]
public sealed record GreetingRequest(
    [property: JsonPropertyName("name")] string Name);
