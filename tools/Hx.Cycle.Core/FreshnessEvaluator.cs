using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public enum StageFreshness
{
    Fresh,
    Stale,
    Completed,
}

/// <summary>
/// The category of a stale verdict (FR-005/006/007) — the machine-readable "why" the restamp-safety classifier
/// (1a) consumes, so refresh can never disagree with this evaluator: <see cref="ChangeSetDiffers"/> (code moved),
/// <see cref="OwnArtifactChanged"/> (the stage's own artifact content changed), <see cref="NotProduced"/> (its
/// produced artifact is absent), <see cref="MissingArtifactBinding"/> (the produced artifact is present but the
/// proof has no bound hash — the produces-binding migration, a safe re-stamp), <see cref="MissingBinding"/> (a
/// runner/schema bump left the prerequisite-artifact binding null — also a safe re-stamp),
/// <see cref="PrereqArtifactChanged"/> (a shared upstream artifact's content hash CHANGED VALUE — a real input
/// change that must force a re-run), and <see cref="PrereqRebindable"/> (027 FR-001: the stage's OWN artifact is
/// unchanged and the ONLY divergence is the prerequisite-artifact binding SET/ORDER, with every shared-path
/// content byte-identical — a pure edge/reorder move that may be auto-rebound, never a content value change).
/// </summary>
public enum StaleReason
{
    ChangeSetDiffers,
    OwnArtifactChanged,
    PrereqArtifactChanged,
    PrereqRebindable,
    MissingArtifactBinding,
    MissingBinding,
    NotProduced,
}

/// <summary>Per-stage freshness verdict computed at read time (never stored). <see cref="StaleReason"/> is the
/// machine-readable category when <see cref="Freshness"/> is <see cref="StageFreshness.Stale"/>; null otherwise.
/// <see cref="ChangedPrereqPaths"/> (028 FR-002) carries the prerequisite artifact PATHS whose bound vs current content
/// hash differs — the exact "what changed" set the self-describing recovery/CLI seam surfaces (and over which it runs a
/// line-level git diff). Additive nullable: null unless the stale reason is prerequisite-content-driven. No git here —
/// the SET is pure; the line-level diff is computed at the CLI/recovery seam.</summary>
public sealed record StageFreshnessResult(
    string Stage,
    StageFreshness Freshness,
    string? Reason,
    StaleReason? StaleReason = null,
    IReadOnlyList<string>? ChangedPrereqPaths = null);

/// <summary>
/// Re-derives whether a stamped stage is still fresh. Code-bound stages must keep the same change-set
/// identity; doc/review stages that happen before implementation are bound by their artifact and
/// prerequisite hashes so normal implementation edits do not stale the entire pre-workflow.
/// </summary>
public sealed class FreshnessEvaluator
{
    private readonly string _repositoryRoot;
    private readonly StageModel _stageModel;

    public FreshnessEvaluator(string repositoryRoot, StageModel stageModel)
    {
        _repositoryRoot = repositoryRoot;
        _stageModel = stageModel;
    }

    public StageFreshnessResult Evaluate(
        CycleStageProof proof,
        string feature,
        string currentIdentity,
        bool requireChangeSetIdentity = true)
    {
        if (requireChangeSetIdentity
            && !string.Equals(proof.ChangeSetId, currentIdentity, StringComparison.Ordinal))
        {
            return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                "change-set identity differs from the current diff (code changed since stamp)",
                StaleReason.ChangeSetDiffers);
        }

        CycleStage stage = _stageModel.Find(proof.Stage);
        if (stage.Produces is { } pattern)
        {
            string artifactPath = StageModel.ResolveProduces(pattern, feature);
            string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            bool present = File.Exists(full);
            if (!present)
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' is absent (never produced)", StaleReason.NotProduced);
            }

            if (proof.ArtifactHashes.Count == 0)
            {
                // Present but never bound: the produces-binding migration (a stage stamped before it had a
                // produces pattern). Re-stamping safely binds the current content — not a content change.
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' is present but unbound; re-stamp with the current runner",
                    StaleReason.MissingArtifactBinding);
            }

            string current = CanonicalArtifactHasher.CanonicalHashOfFile(full);
            if (!proof.ArtifactHashes.Contains(current))
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' changed since stamp", StaleReason.OwnArtifactChanged);
            }
        }

        // Living-Spec (FR-027): a stage also binds the canonical CONTENT of its transitive prerequisite
        // artifacts, so editing the spec stales a dependent plan/tasks even though its own file is
        // untouched — forcing re-clarify/re-analyze. Re-stamping an upstream stage WITHOUT a content change
        // does NOT stale it (the binding is to content, not the upstream proof), removing the re-stamp cascade.
        IReadOnlyList<string> currentPrereqs =
            CanonicalArtifactHasher.PrerequisiteArtifactHashes(_repositoryRoot, _stageModel, proof.Stage, feature);
        if (currentPrereqs.Count > 0)
        {
            if (proof.PrerequisiteArtifactHashes is null)
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    "missing prerequisite artifact binding; re-stamp with the current runner",
                    StaleReason.MissingBinding);
            }

            if (!proof.PrerequisiteArtifactHashes.SequenceEqual(currentPrereqs, StringComparer.Ordinal))
            {
                IReadOnlyList<string> changedPaths =
                    DivergingPrereqPaths(proof.PrerequisiteArtifactHashes, currentPrereqs);

                // 028 FR-005: the decay arm. A ReviewedNoImpactRebound proof was rebound to the upstream content the
                // agent attested. It is an ordinary content-bound proof (no snapshot), so a LATER upstream edit must
                // re-stale it as PrereqArtifactChanged — never the auto-rebindable PrereqRebindable tier (the rebind
                // decision is the agent's, not the cascade's). Force the agent-gated arm and surface the changed paths.
                if (string.Equals(proof.Outcome, CycleStageOutcome.ReviewedNoImpactRebound, StringComparison.Ordinal))
                {
                    return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                        "a prerequisite artifact changed since the reviewed-no-impact rebind; re-review the dependent",
                        StaleReason.PrereqArtifactChanged, changedPaths);
                }

                // 027 FR-001: split the prereq stale. The own-artifact arm above did NOT fire (own content
                // unchanged), so the divergence is purely in the prerequisite binding. If every SHARED path's
                // content hash is byte-identical (only the prereq SET/ORDER moved — an edge/reorder), AND the
                // stage is not change-set-bound, this is a PrereqRebindable lag the chokepoint may auto-rebind
                // once all producing upstreams are Fresh; otherwise a shared path's content VALUE changed —
                // a real input change that stays PrereqArtifactChanged (RerunRequired). Fail-closed: anything
                // that is not provably a pure edge/reorder falls through to PrereqArtifactChanged.
                bool rebindable = !requireChangeSetIdentity
                    && SharedPrereqContentIdentical(proof.PrerequisiteArtifactHashes, currentPrereqs);
                return rebindable
                    ? new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                        "prerequisite binding moved (edge/reorder) but every shared artifact is byte-identical; re-stamp re-binds",
                        StaleReason.PrereqRebindable, changedPaths)
                    : new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                        "a prerequisite artifact changed since stamp; re-clarify/re-analyze the dependent",
                        StaleReason.PrereqArtifactChanged, changedPaths);
            }
        }

        return new StageFreshnessResult(proof.Stage, StageFreshness.Fresh, null);
    }

    /// <summary>
    /// 027 FR-001: true when the ONLY difference between the bound and the current prerequisite-artifact sets is
    /// which paths are present (a SET/ORDER move) — i.e. every path present in BOTH binds the SAME canonical
    /// content hash. A path whose hash differs between the two sets is a content VALUE change and makes this
    /// false (so the caller keeps <see cref="StaleReason.PrereqArtifactChanged"/>). Entries are the
    /// <c>&lt;relativePath&gt;:&lt;hash&gt;</c> form produced by <see cref="CanonicalArtifactHasher.PrerequisiteArtifactHashes"/>.
    /// </summary>
    private static bool SharedPrereqContentIdentical(
        IReadOnlyList<string> bound, IReadOnlyList<string> current)
    {
        Dictionary<string, string> boundByPath = ToPathHashMap(bound);
        Dictionary<string, string> currentByPath = ToPathHashMap(current);
        foreach (KeyValuePair<string, string> entry in currentByPath)
        {
            if (boundByPath.TryGetValue(entry.Key, out string? boundHash)
                && !string.Equals(boundHash, entry.Value, StringComparison.Ordinal))
            {
                return false; // a shared path's content hash changed value — not a pure edge/reorder
            }
        }

        return true;
    }

    /// <summary>
    /// 028 FR-002: the prerequisite artifact PATHS whose bound vs current content hash differs — a path present in only
    /// one set, or present in both with a different hash. Sorted-ordinal (deterministic). The keys are the
    /// <c>&lt;relativePath&gt;</c> portion of the <c>&lt;relativePath&gt;:&lt;hash&gt;</c> entries. Pure — no git.
    /// </summary>
    private static IReadOnlyList<string> DivergingPrereqPaths(
        IReadOnlyList<string> bound, IReadOnlyList<string> current)
    {
        Dictionary<string, string> boundByPath = ToPathHashMap(bound);
        Dictionary<string, string> currentByPath = ToPathHashMap(current);
        var changed = new SortedSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in currentByPath)
        {
            if (!boundByPath.TryGetValue(entry.Key, out string? boundHash)
                || !string.Equals(boundHash, entry.Value, StringComparison.Ordinal))
            {
                changed.Add(entry.Key);
            }
        }

        foreach (string boundPath in boundByPath.Keys)
        {
            if (!currentByPath.ContainsKey(boundPath))
            {
                changed.Add(boundPath);
            }
        }

        return changed.ToList();
    }

    private static Dictionary<string, string> ToPathHashMap(IReadOnlyList<string> entries)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string entry in entries)
        {
            int sep = entry.LastIndexOf(':');
            string path = sep >= 0 ? entry[..sep] : entry;
            string hash = sep >= 0 ? entry[(sep + 1)..] : entry;
            map[path] = hash;
        }

        return map;
    }
}
