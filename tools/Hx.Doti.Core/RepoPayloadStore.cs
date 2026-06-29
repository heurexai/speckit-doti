using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 022 T012 (FR-001): the single read/write authority for a repo's <c>.doti/payload.json</c>
/// (<see cref="RepoPayloadStamp"/>) — the recorded Doti payload + tool version. Extracted from
/// <see cref="DotiInstaller"/>'s private read/stamp so the version-lifecycle commands
/// (<c>check-version</c>/<c>scan</c>/<c>update</c>) and the installer share ONE serialization shape and ONE
/// atomic-write path. Reads are fail-soft (absent/malformed → <c>null</c>); the write is crash-safe (temp + move).
/// </summary>
public static class RepoPayloadStore
{
    public const string RelativePath = ".doti/payload.json";

    /// <summary>The full recorded stamp, or <c>null</c> when the file is absent or malformed.</summary>
    public static RepoPayloadStamp? Read(string repoRoot)
    {
        string path = FullPath(repoRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RepoPayloadStamp>(
                File.ReadAllText(path), JsonContractSerializerOptions.Create());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The recorded payload version, or <c>null</c> when absent/malformed/blank.</summary>
    public static string? ReadPayloadVersion(string repoRoot) =>
        Read(repoRoot)?.PayloadVersion is { Length: > 0 } v ? v : null;

    /// <summary>
    /// Stamp <c>.doti/payload.json</c> with the payload + tool version, written atomically (temp + move) so a crash
    /// re-runs to a clean state. The schema is <see cref="RepoPayloadStamp.CurrentSchemaVersion"/>.
    /// </summary>
    public static void Write(string repoRoot, string payloadVersion, string toolVersion)
    {
        string path = FullPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        string json = JsonSerializer.Serialize(
            new RepoPayloadStamp(RepoPayloadStamp.CurrentSchemaVersion, payloadVersion, toolVersion), options);

        string temp = path + ".tmp-" + Guid.NewGuid().ToString("n");
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    private static string FullPath(string repoRoot) =>
        Path.GetFullPath(Path.Combine(repoRoot, RelativePath.Replace('/', Path.DirectorySeparatorChar)));
}
