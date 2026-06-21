using System.Text.Json;
using Hx.Runner.Core.Io;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Tools;

/// <summary>
/// The shared, versioned, RID-keyed tool store. Vendored tool binaries live here ONCE per machine/user
/// instead of being copied into every generated solution. Layout:
/// <c>&lt;root&gt;/&lt;tool&gt;/&lt;version&gt;/&lt;rid&gt;/&lt;executableName&gt;</c>. The root is per-user
/// (<c>%LOCALAPPDATA%/Heurex/speckit-doti/tools</c> on Windows; the XDG data dir elsewhere) and can be
/// overridden with the <c>HX_TOOL_STORE</c> environment variable (machine-global / CI). Installs are
/// additive and recorded in a <c>.store-manifest.json</c> index. Writes are serialized in-process and
/// published atomically (temp file + move), so a concurrent reader never sees a partial index.
/// </summary>
public static class ToolStore
{
    public const string OverrideEnvVar = "HX_TOOL_STORE";
    public const string IndexFileName = ".store-manifest.json";

    private static readonly object IndexGate = new();

    /// <summary>The store root, honoring <c>HX_TOOL_STORE</c> then the per-OS user data location.</summary>
    public static string Root()
    {
        string? overridePath = Environment.GetEnvironmentVariable(OverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        string baseDir;
        if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else
        {
            string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? string.Empty;
            baseDir = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(baseDir, "Heurex", "speckit-doti", "tools");
    }

    public static string EntryDirectory(string tool, string version, string rid) =>
        Path.Combine(Root(), Safe(tool), Safe(version), Safe(rid));

    public static string PathFor(string tool, string version, string rid, string executableName) =>
        Path.Combine(EntryDirectory(tool, version, rid), executableName);

    /// <summary>True when the store holds the executable AND its SHA-256 matches <paramref name="expectedSha256"/>.</summary>
    public static bool IsInstalled(string tool, string version, string rid, string executableName, string expectedSha256)
    {
        string path = PathFor(tool, version, rid, executableName);
        return File.Exists(path) &&
            string.Equals(FileHashing.Sha256OfFile(path), expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Read the store index (empty when the store has never been written).</summary>
    public static ToolStoreIndex ReadIndex()
    {
        string indexPath = Path.Combine(Root(), IndexFileName);
        if (!File.Exists(indexPath))
        {
            return new ToolStoreIndex(JsonContractDefaults.SchemaVersion, []);
        }

        return JsonSerializer.Deserialize<ToolStoreIndex>(File.ReadAllText(indexPath), JsonContractSerializerOptions.Create())
            ?? new ToolStoreIndex(JsonContractDefaults.SchemaVersion, []);
    }

    /// <summary>Upsert (by tool+version+rid+executableName) an entry into the index, published atomically.</summary>
    public static void RecordEntry(ToolStoreEntry entry)
    {
        lock (IndexGate)
        {
            string root = Root();
            Directory.CreateDirectory(root);
            string indexPath = Path.Combine(root, IndexFileName);

            List<ToolStoreEntry> entries = ReadIndex().Entries
                .Where(e => !(e.Tool == entry.Tool && e.Version == entry.Version && e.Rid == entry.Rid && e.ExecutableName == entry.ExecutableName))
                .Append(entry)
                .ToList();

            JsonSerializerOptions options = JsonContractSerializerOptions.Create();
            options.WriteIndented = true;
            string tmp = indexPath + ".tmp-" + Guid.NewGuid().ToString("n");
            File.WriteAllText(tmp, JsonSerializer.Serialize(new ToolStoreIndex(JsonContractDefaults.SchemaVersion, entries), options));
            File.Move(tmp, indexPath, overwrite: true);
        }
    }

    private static string Safe(string segment)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            segment = segment.Replace(c, '_');
        }

        return segment;
    }
}
