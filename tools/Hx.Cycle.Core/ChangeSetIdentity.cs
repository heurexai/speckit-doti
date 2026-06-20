using Hx.Impact.Core.ChangeDetection;
using Hx.Runner.Core.Io;

namespace Hx.Cycle.Core;

/// <summary>
/// A deterministic identity for a change set: the sorted (Ordinal) changed-path set, each path paired
/// with its current content hash, all hashed together. Same inputs ⇒ identical identity; a content edit
/// to any changed file ⇒ a different identity (the property freshness detection relies on). Deleted or
/// absent paths hash as the literal <c>absent</c> so a removal still moves the identity.
/// </summary>
public static class ChangeSetIdentity
{
    /// <summary>Pure over the supplied paths + the files they name (the unit the tests pin for determinism).</summary>
    public static string Compute(string repositoryRoot, IReadOnlyList<string> changedPaths)
    {
        var lines = new List<string>(changedPaths.Count);
        foreach (string path in changedPaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            string full = Path.GetFullPath(Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)));
            string hash = File.Exists(full) ? FileHashing.Sha256OfFile(full) : "absent";
            lines.Add($"{path}\t{hash}");
        }

        return FileHashing.Sha256OfText(string.Join("\n", lines));
    }

    /// <summary>Live: collect the change set for <c>base..head</c> (∪ working tree), then compute. Fails closed
    /// (throws) if the merge-base cannot be resolved — never returns a misleading identity.</summary>
    public static string Of(string repositoryRoot, string baseRef, string headRef) =>
        Compute(repositoryRoot, new ImpactChangeCollector().Collect(repositoryRoot, baseRef, headRef));
}
