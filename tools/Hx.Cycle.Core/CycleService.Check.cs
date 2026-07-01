using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    public CycleStatusReport Status()
    {
        var recovery = RecoverStateIfNeeded();
        CycleState state = recovery.State
            ?? throw new InvalidOperationException(
                $"No cycle state at {CycleStateStore.RelativePath}; run `doti cycle stamp --stage <id> --feature <NNN-slug>` first.");

        if (TryCompletedClean(state, out CycleCompletionRecord? completion))
        {
            List<StageFreshnessResult> completed = state.Stages
                .Select(proof => new StageFreshnessResult(proof.Stage, StageFreshness.Completed,
                    $"cycle completed at commit {completion.CommitSha}"))
                .ToList();
            return new CycleStatusReport(JsonContractDefaults.SchemaVersion, state, completed, completion, recovery.Report, BuildReleaseTrain(state));
        }

        string identity = FreshnessIdentity(state);
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);
        List<StageFreshnessResult> freshness = state.Stages
            .Select(proof => evaluator.Evaluate(
                proof,
                state.Feature,
                identity,
                RequiresChangeSetIdentity(proof.Stage)))
            .ToList();

        return new CycleStatusReport(JsonContractDefaults.SchemaVersion, state, freshness, state.Completion, recovery.Report, BuildReleaseTrain(state));
    }

    /// <summary>Fail-closed chokepoint: every transitive prerequisite of <paramref name="stageId"/> must be
    /// stamped, fresh (artifact + change-set identity unchanged), and valid (a doc prerequisite has no open
    /// [NEEDS CLARIFICATION] marker). The CLI exits non-zero unless <see cref="CycleCheckReport.Passed"/>.</summary>
    public CycleCheckReport Check(string stageId) => Check(stageId, null);

    /// <inheritdoc cref="Check(string)"/>
    /// <param name="excludedOwnedPaths">Extra owned artifact paths (e.g. an INCOMING feature's just-written spec on a
    /// new-feature start) subtracted from the freshness change-set identity, on top of the current feature's own
    /// owned paths. So a stage's in-flight artifact never falsely stales a diff-kind prerequisite.</param>
    public CycleCheckReport Check(string stageId, IReadOnlyList<string>? excludedOwnedPaths)
    {
        CycleStage target = _stageModel.Find(stageId); // fail-closed on an unknown stage
        var recovery = RecoverStateIfNeeded();
        CycleState? state = recovery.State;
        if (recovery.Report.Verdict == CycleRecoveryVerdict.Ambiguous)
        {
            return new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, false,
                [new StagePrereqResult("commit-recovery", "ambiguous", false, recovery.Report.Reason)],
                state?.Completion,
                recovery.Report);
        }

        if (TryCompletedCycleCheck(target, state, recovery.Report, out CycleCheckReport? completedReport))
        {
            return completedReport!;
        }

        // 038 (bug-only-release-cycle-check): a bug-only repo has NO .doti/cycle-state.json, so `state` is null. Without
        // this branch the transitive-prerequisite loop below marks every feature stage (specify..drift-review) "missing"
        // — a FALSE blocker that has misled agents into fabricating a feature cycle (the exact 033/034/037 anti-pattern).
        // The 033 bug-only tolerance (BuildReleaseTrain admits a null state + consults the injected bug members) was
        // never extended to THIS chokepoint: the release-train block at the end of Check is guarded by `state is not
        // null`, so a bug-only repo never reaches it. For a bug-only RELEASE, delegate readiness ENTIRELY to the
        // bug-aware release train — the same evaluation GetReleaseTrain/MarkReleaseTrainReleased already trust — instead
        // of feature prerequisites a bug-only repo neither has nor should fabricate. A valid bug-only train (>=1
        // test-passed, unreleased bug) passes; an empty/invalid one fails closed on the train's own blocker.
        if (state is null && string.Equals(target.Id, "release", StringComparison.OrdinalIgnoreCase))
        {
            return BugOnlyReleaseCheck(target, recovery.Report);
        }

        string identity = FreshnessIdentity(state, excludedOwnedPaths);
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);

        var results = new List<StagePrereqResult>();
        foreach (string prereqId in ResolveTransitivePrerequisites(target))
        {
            results.Add(EvaluatePrerequisite(prereqId, state, evaluator, identity));
        }

        CycleReleaseTrain? releaseTrain = null;
        if (string.Equals(target.Id, "release", StringComparison.OrdinalIgnoreCase) && state is not null)
        {
            releaseTrain = BuildReleaseTrain(state);
            if (!releaseTrain.Valid)
            {
                foreach (string blocker in releaseTrain.Blockers)
                {
                    results.Add(new StagePrereqResult("release-train", "invalid", false, blocker));
                }
            }
        }

        bool passed = results.All(r => r.Ok);
        return new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, passed, results, state?.Completion, recovery.Report, releaseTrain);
    }

    /// <summary>
    /// 038: readiness of a bug-only RELEASE (a repo with no <c>.doti/cycle-state.json</c>) IS the bug-aware release
    /// train — there are no feature-cycle prerequisites to check, and fabricating them is the 033/034/037 anti-pattern.
    /// Delegates to <see cref="BuildReleaseTrain"/> with the null state (its 033 tolerance yields an empty feature half
    /// and consults the injected bug members): a valid train (>=1 test-passed, unreleased bug) passes with a single
    /// stand-in "ready" result; an invalid/empty one fails closed, surfacing the train's own blockers (e.g. "no
    /// completed unreleased ... ready for release") — a genuinely-empty repo is still rejected.
    /// </summary>
    private CycleCheckReport BugOnlyReleaseCheck(CycleStage target, CycleRecoveryReport recoveryReport)
    {
        CycleReleaseTrain train = BuildReleaseTrain(null);
        List<StagePrereqResult> results = train.Valid
            ? [new StagePrereqResult("release-train", "ready", true,
                "bug-only release: a valid bug release train stands in for feature-cycle prerequisites")]
            : train.Blockers
                .Select(blocker => new StagePrereqResult("release-train", "invalid", false, blocker))
                .ToList();
        return new CycleCheckReport(
            JsonContractDefaults.SchemaVersion, target.Id, train.Valid, results, null, recoveryReport, train);
    }

    /// <summary>The change-set identity used for prerequisite-FRESHNESS evaluation: the live <c>base..HEAD ∪ working
    /// tree</c> set with the cycle's OWN doc/review artifacts subtracted. A stage's in-flight owned artifact — the /08
    /// drift-review report staged for the release transition, or an incoming feature's just-written spec on a
    /// new-feature start (<paramref name="additionalExcluded"/>) — must not move the identity that decides whether a
    /// DIFF-kind prerequisite (implement) is still fresh. Safe: the only diff-kind stage produces no artifact, so no
    /// code edit is ever subtracted; doc/review artifacts are bound by their own proof hashes, not this identity.</summary>
    private string FreshnessIdentity(CycleState? state, IReadOnlyList<string>? additionalExcluded = null) =>
        StageChangeSetIdentity(
            state?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot), state?.Feature, additionalExcluded);

    /// <summary>
    /// 026: the SINGLE source of a stage proof's change-set identity — <c>base..HEAD</c> with the feature's OWN
    /// doc/review artifacts subtracted. The stamp, the transition-rebase, AND the freshness check MUST all use this
    /// same computation. Before 026 the stamp/rebase stored the RAW identity while the check subtracted owned paths
    /// (the 021 fix only reached the check), so a change-set-bound stage (implement/drift-review/release) read a FALSE
    /// stale against its own in-range owned doc — an identity the owned-path-excluding check could never match.
    /// </summary>
    private string StageChangeSetIdentity(
        string baseRef, string? feature, IReadOnlyList<string>? additionalExcluded = null)
    {
        var excluded = new List<string>();
        if (!string.IsNullOrWhiteSpace(feature))
        {
            excluded.AddRange(FeatureArtifactScope.OwnedPaths(_stageModel, feature));
        }

        if (additionalExcluded is { Count: > 0 })
        {
            excluded.AddRange(additionalExcluded);
        }

        return ChangeSetIdentity.Of(_repositoryRoot, baseRef, "HEAD", excluded);
    }

}
