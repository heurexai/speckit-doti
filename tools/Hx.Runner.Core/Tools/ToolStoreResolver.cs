using Hx.Runner.Core.Io;

namespace Hx.Runner.Core.Tools;

/// <summary>
/// Resolves a vendored tool's executable from the shared <see cref="ToolStore"/>, verifying its SHA-256
/// against the manifest. Returns the verified store path, or <c>null</c> when the store has no matching,
/// verified entry — callers then fall back to the in-repo path and ultimately fail closed.
///
/// This is intentionally a SEPARATE resolver from <see cref="Repository.RepositoryPathResolver"/>: the
/// in-repo escape guard there is never loosened to point at an out-of-repo store. A tool is trusted from
/// the store only when its bytes hash to the manifest's pinned value — the same guarantee the gate enforces.
/// </summary>
public static class ToolStoreResolver
{
    public static string? Resolve(string tool, string version, string rid, string executableName, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(expectedSha256) || string.IsNullOrWhiteSpace(executableName))
        {
            return null;
        }

        string path = ToolStore.PathFor(tool, version, rid, executableName);
        if (File.Exists(path) &&
            string.Equals(FileHashing.Sha256OfFile(path), expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return null;
    }
}
