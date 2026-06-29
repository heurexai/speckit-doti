using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 022 T021 (FR-001/002/004): read one repo's Doti version and relate it to the installed tool. Read-only.
/// Distinguishes the two "no version" cases the user hit — a directory that is NOT a Doti repo (no <c>.doti</c>)
/// from a Doti repo whose <c>.doti/payload.json</c> is missing/unreadable (<c>version-unknown</c>) — so both the
/// human line and the agent JSON explain the absence instead of silently reporting <c>unknown</c>. Composes
/// <see cref="RepoPayloadStore"/> (the read) + <see cref="DotiVersionRelationCalculator"/> (the single-sourced relation).
/// </summary>
public static class DotiVersionInspector
{
    public static DotiRepoVersion Inspect(string repoRoot, string installedToolVersion)
    {
        string root = Path.GetFullPath(repoRoot);
        if (!Directory.Exists(Path.Combine(root, ".doti")))
        {
            return new DotiRepoVersion(
                JsonContractDefaults.SchemaVersion, root, DotiVersionStatus.NotARepo,
                null, null, installedToolVersion, DotiVersionRelation.Unknown,
                "No .doti directory — not a Doti-enabled repository.");
        }

        RepoPayloadStamp? stamp = RepoPayloadStore.Read(root);
        if (stamp?.PayloadVersion is not { Length: > 0 } payloadVersion)
        {
            return new DotiRepoVersion(
                JsonContractDefaults.SchemaVersion, root, DotiVersionStatus.VersionUnknown,
                null, null, installedToolVersion, DotiVersionRelation.Unknown,
                "Doti repo, but .doti/payload.json is missing or has no payload version (run `hx doti update`).");
        }

        DotiVersionRelation relation = DotiVersionRelationCalculator.Relate(payloadVersion, installedToolVersion);
        return new DotiRepoVersion(
            JsonContractDefaults.SchemaVersion, root, DotiVersionStatus.Ok,
            payloadVersion, stamp.ToolVersion, installedToolVersion, relation, null);
    }
}
