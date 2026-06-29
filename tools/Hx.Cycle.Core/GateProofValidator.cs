using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Graph;
using Hx.Impact.Core.Planning;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public static class GateProofValidator
{
    /// <summary>
    /// 023-bug fix: <paramref name="headRef"/> bounds the affected-test re-plan to a CONCRETE commit instead of the
    /// live symbolic <c>HEAD</c>. Transitions pass <c>null</c> (re-plan to live <c>HEAD</c> — the transition IS at
    /// HEAD). The RELEASE-TRAIN passes the feature's own release commit (<c>completion.CommitSha</c>), so a later
    /// unrelated commit landing on top — e.g. a separate bug fix — cannot move the re-plan endpoint and falsely
    /// invalidate an UNCHANGED feature's proof ("planner hash does not match the current change set"). A feature's
    /// proof is bound by its own diff (<c>BaseRef..CommitSha</c>), not by where the branch tip happens to be now.
    /// </summary>
    public static IReadOnlyList<string> ValidateAffectedTestProof(
        string repositoryRoot, PersistedGateProof persisted, string? headRef = null)
    {
        AffectedTestProof? proof = persisted.Proof.AffectedTestProof;
        if (proof is null)
        {
            return ["gate proof has no affected-test proof; re-run `gate run` with the current runner"];
        }

        string replanHead = string.IsNullOrWhiteSpace(headRef) ? proof.HeadRef : headRef;

        var reasons = new List<string>();
        if (proof.SchemaVersion != JsonContractDefaults.SchemaVersion)
        {
            reasons.Add($"affected-test proof schema version {proof.SchemaVersion} is unsupported");
        }

        if (!RefsResolveToSameCommit(repositoryRoot, proof.BaseRef, persisted.BaseRef))
        {
            reasons.Add("affected-test proof base ref does not match the persisted gate proof");
        }

        if (!string.Equals(proof.HeadRef, "HEAD", StringComparison.Ordinal))
        {
            reasons.Add("affected-test proof head ref must be HEAD");
        }

        if (proof.ExecutedTests.Any(t => t.ExitCode != 0 || t.Outcome != StageOutcome.Pass))
        {
            reasons.Add("affected-test proof contains a failed test execution");
        }

        string selfPlanHash = AffectedTestProofHasher.HashPlan(proof.Plan);
        if (!string.Equals(selfPlanHash, proof.PlanHash, StringComparison.Ordinal))
        {
            reasons.Add("affected-test proof plan hash does not match the embedded plan");
        }

        string selfExecutedHash = AffectedTestProofHasher.HashExecutedTests(proof.ExecutedTests);
        if (!string.Equals(selfExecutedHash, proof.ExecutedTestsHash, StringComparison.Ordinal))
        {
            reasons.Add("affected-test proof execution hash does not match the embedded executions");
        }

        try
        {
            AffectedPlan expectedPlan = new AffectedTestPlanner().Plan(
                repositoryRoot, persisted.BaseRef, replanHead, proof.Configuration);
            string expectedPlanHash = AffectedTestProofHasher.HashPlan(expectedPlan);
            if (!string.Equals(expectedPlanHash, proof.PlanHash, StringComparison.Ordinal))
            {
                reasons.Add("affected-test proof is stale or forged: planner hash does not match the current change set");
            }

            bool expectedFullSuite = persisted.Lane == Lane.Release || expectedPlan.Outcome == AffectedOutcome.FullGateRequired;
            if (proof.FullSuite != expectedFullSuite)
            {
                reasons.Add(expectedFullSuite
                    ? "affected-test proof did not record the required full test suite"
                    : "affected-test proof unexpectedly records a full-suite run for a narrowed plan");
            }

            IReadOnlyList<string> expectedProjects = expectedFullSuite
                ? AllTestProjectPaths(repositoryRoot)
                : expectedPlan.SelectedTests.Select(t => t.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string expectedScopeHash = AffectedTestProofHasher.HashTestScope(expectedProjects);
            if (!string.Equals(expectedScopeHash, proof.TestScopeHash, StringComparison.Ordinal))
            {
                reasons.Add("affected-test proof selected test-scope hash does not match the expected planner scope");
            }

            string[] executedProjects = proof.ExecutedTests
                .Select(t => Normalize(t.ProjectPath))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] normalizedExpected = expectedProjects
                .Select(Normalize)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!executedProjects.SequenceEqual(normalizedExpected, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("affected-test proof executions do not match the expected test project set");
            }
        }
        catch (Exception ex)
        {
            reasons.Add("could not recompute affected-test proof: " + ex.Message);
        }

        return reasons;
    }

    /// <summary>
    /// FR-029 downgrade guard: re-resolve the repo's declared tier ladder and verify the proof was minted under
    /// it. A proof whose recorded tier or ladder coverage does not match the current declared tier — i.e. a
    /// silently narrowed/downgraded ladder (editing <c>integration.json</c> to a weaker tier, or a gate's mode to
    /// advisory/skip) — cannot mint a passing proof. A pre-FR-029 proof (no tier binding) requires a re-run.
    /// </summary>
    public static IReadOnlyList<string> ValidateLadderCoverage(string repositoryRoot, PersistedGateProof persisted)
    {
        GateProof proof = persisted.Proof;
        GateLadderResolution resolution = GateLadderResolver.Resolve(repositoryRoot);
        if (!resolution.Ok)
        {
            return ["the repo's gate tier no longer resolves: " + (resolution.FailureReason ?? "unknown")];
        }

        if (proof.Tier is null || proof.LadderCoverage is null)
        {
            return ["gate proof predates tier-ladder binding; re-run `gate run` with the current runner"];
        }

        GateLadder expected = resolution.Ladder!;
        var reasons = new List<string>();
        if (!string.Equals(proof.Tier, expected.Tier, StringComparison.Ordinal))
        {
            reasons.Add($"gate proof tier '{proof.Tier}' does not match the repo's declared tier '{expected.Tier}' (a tier downgrade cannot mint a passing proof)");
        }

        if (!CoverageEqual(proof.LadderCoverage, expected.Coverage()))
        {
            reasons.Add("gate proof ladder coverage does not match the repo's declared tier ladder (a narrowed/downgraded ladder cannot mint a passing proof)");
        }

        return reasons;
    }

    /// <summary>
    /// FR-028 (M-1) scope guard: recompute the docs-only scope from the CURRENT change set and verify the proof's
    /// recorded scope matches. A scope skip (architecture + Sentrux not run) can never be minted for a change that is
    /// not docs-only — distinct from the tier-ladder check, recomputed separately, provable-not-bypassed. A pre-FR-028
    /// proof (no scope dimension) is not blocked here; its test scope is still gated by the affected-test proof.
    /// </summary>
    public static IReadOnlyList<string> ValidateScope(
        string repositoryRoot, PersistedGateProof persisted, string? headRef = null)
    {
        GateProof proof = persisted.Proof;
        if (proof.Scope is null || proof.AffectedTestProof is null)
        {
            return [];
        }

        try
        {
            // 023-bug fix: bound the scope re-resolve to the same concrete head as the affected-test re-plan
            // (see ValidateAffectedTestProof) so a later commit on top cannot invalidate an unchanged feature.
            string replanHead = string.IsNullOrWhiteSpace(headRef) ? "HEAD" : headRef;
            GateScope expected = GateScopeResolver.Resolve(
                repositoryRoot, persisted.BaseRef, replanHead, proof.AffectedTestProof.Plan);
            return ScopeIsValid(proof.Scope.DocsOnly, expected.DocsOnly)
                ? []
                : [$"gate proof records a docs-only scope skip (architecture + Sentrux not run) but the current change set is NOT docs-only — a scope skip cannot be minted for a code change (FR-028)"];
        }
        catch (Exception ex)
        {
            return ["could not recompute gate scope: " + ex.Message];
        }
    }

    /// <summary>
    /// FR-028 (M-1) scope validity (pure): a recorded gate scope is valid against the change set's ACTUAL docs-only
    /// status iff it does not claim a docs-only SKIP for a non-docs-only change. A full-gate proof
    /// (<paramref name="recordedDocsOnly"/> false) ran architecture + Sentrux, so it is valid for ANY change — including
    /// a docs-only one (over-strict, never a forged skip; this is what lets a docs-only release validate against the
    /// always-full release lane). A docs-only skip (<paramref name="recordedDocsOnly"/> true) is valid ONLY when the
    /// change is genuinely docs-only — a code change can never inherit the skip.
    /// </summary>
    public static bool ScopeIsValid(bool recordedDocsOnly, bool actualDocsOnly) =>
        !recordedDocsOnly || actualDocsOnly;

    private static bool CoverageEqual(IReadOnlyList<GateLadderEntry> recorded, IReadOnlyList<GateLadderEntry> expected)
    {
        if (recorded.Count != expected.Count)
        {
            return false;
        }

        GateLadderEntry[] a = recorded.OrderBy(e => e.Step, StringComparer.Ordinal).ToArray();
        GateLadderEntry[] b = expected.OrderBy(e => e.Step, StringComparer.Ordinal).ToArray();
        for (int i = 0; i < a.Length; i++)
        {
            if (!string.Equals(a[i].Step, b[i].Step, StringComparison.Ordinal)
                || !string.Equals(a[i].Mode, b[i].Mode, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> AllTestProjectPaths(string repositoryRoot)
    {
        string[] solutions = Directory.GetFiles(repositoryRoot, "*.slnx");
        if (solutions.Length != 1)
        {
            return [];
        }

        ProjectGraph graph = new ProjectGraphBuilder().Build(repositoryRoot, Path.GetFileName(solutions[0]));
        return graph.Nodes.Values
            .Where(n => n.IsTestProject)
            .Select(n => n.Path)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static bool RefsResolveToSameCommit(string repositoryRoot, string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        string? leftSha = TryResolve(repositoryRoot, left);
        string? rightSha = TryResolve(repositoryRoot, right);
        return !string.IsNullOrWhiteSpace(leftSha)
            && string.Equals(leftSha, rightSha, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryResolve(string repositoryRoot, string reference)
    {
        ProcessRunResult result = ProcessRunner.Run(new ToolCommand("git", ["rev-parse", reference], repositoryRoot));
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }
}
