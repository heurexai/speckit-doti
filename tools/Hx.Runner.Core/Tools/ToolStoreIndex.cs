using System.Text.Json.Serialization;

namespace Hx.Runner.Core.Tools;

/// <summary>One installed entry in the shared tool store (the <c>.store-manifest.json</c> index).</summary>
public sealed record ToolStoreEntry(
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("rid")] string Rid,
    [property: JsonPropertyName("executableName")] string ExecutableName,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("source")] string Source);

/// <summary>The shared tool store's own index of installed entries.</summary>
public sealed record ToolStoreIndex(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("entries")] IReadOnlyList<ToolStoreEntry> Entries);
