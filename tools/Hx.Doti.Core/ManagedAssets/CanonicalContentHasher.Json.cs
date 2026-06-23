using System.Text;
using System.Text.Json;

namespace Hx.Doti.Core.ManagedAssets;

public static partial class CanonicalContentHasher
{
    private static string CanonicalizeJson(byte[] bytes)
    {
        string text = DecodeUtf8(bytes);
        using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteJson(document.RootElement, writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJson(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!seen.Add(property.Name))
                    {
                        throw new InvalidOperationException($"JSON object contains duplicate property '{property.Name}'.");
                    }

                    writer.WritePropertyName(property.Name);
                    WriteJson(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteJson(item, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                WriteJsonNumber(element, writer);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static void WriteJsonNumber(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.TryGetInt64(out long integer))
        {
            const long maxSafeInteger = 9_007_199_254_740_991;
            if (integer is > maxSafeInteger or < -maxSafeInteger)
            {
                throw new InvalidOperationException("JSON number is outside the managed-asset canonical safe integer range.");
            }

            writer.WriteNumberValue(integer);
            return;
        }

        if (!element.TryGetDouble(out double number) || double.IsNaN(number) || double.IsInfinity(number))
        {
            throw new InvalidOperationException("JSON number is not representable as a finite double for canonical hashing.");
        }

        writer.WriteNumberValue(number);
    }
}
