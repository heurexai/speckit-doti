using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>The <c>doti cycle status</c> output: the persisted state plus a freshness verdict per stamped stage.</summary>
public sealed record CycleStatusReport(int SchemaVersion, CycleState State, IReadOnlyList<StageFreshnessResult> Freshness);

/// <summary>One prerequisite's verdict in a <c>cycle check</c>: the stage, its status (fresh|stale|missing|invalid), and a reason.</summary>
public sealed record StagePrereqResult(string Stage, string Status, bool Ok, string? Reason);

/// <summary>The <c>doti cycle check</c> output: the checked stage, whether all prerequisites passed, and the per-prereq detail.</summary>
public sealed record CycleCheckReport(int SchemaVersion, string Stage, bool Passed, IReadOnlyList<StagePrereqResult> Prerequisites);

/// <summary>The <c>doti cycle commit</c> output: whether the commit was performed, the sha if so, and the refusal reasons if not.</summary>
public sealed record CycleCommitResult(int SchemaVersion, bool Committed, string? CommitSha, IReadOnlyList<string> Reasons);

/// <summary>
/// Records + reports cycle state (<see cref="Stamp"/>/<see cref="Status"/> — non-enforcing) and
/// <b>enforces</b> it: <see cref="Check"/> is the fail-closed chokepoint (every transitive
/// prerequisite stamped + fresh + valid) and <see cref="Commit"/> is the sanctioned commit path (refuses
/// unless the prerequisites, the persisted gate proof, and the staged scope are all clean). Fails closed
/// (throws) only on a genuine execution error; otherwise returns a verdict the CLI maps to an exit code.
/// </summary>
public sealed class CycleService
{
    private readonly string _repositoryRoot;
    private readonly CycleStateStore _store;
    private readonly StageModel _stageModel;

    public CycleService(string repositoryRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _store = new CycleStateStore(_repositoryRoot);
        _stageModel = StageModel.Load(Path.Combine(_repositoryRoot, ".doti", "workflows", "doti", "workflow.yml"));
    }

    public CycleState Stamp(string stageId, string? feature, string? baseRef)
    {
        CycleStage stage = _stageModel.Find(stageId); // fail-closed on an unknown stage
        CycleState? existing = _store.Read();

        string resolvedFeature = feature
            ?? existing?.Feature
            ?? throw new InvalidOperationException(
                "No feature set for the cycle; pass --feature <slug> on the first stamp (e.g. phase-14-doti-cycle-state).");
        string resolvedBaseRef = baseRef ?? existing?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot);

        if (stage.Prereqs.Count > 0)
        {
            CycleCheckReport prereqCheck = Check(stage.Id);
            if (!prereqCheck.Passed)
            {
                string summary = string.Join("; ", prereqCheck.Prerequisites
                    .Where(p => !p.Ok)
                    .Select(p => $"{p.Stage}: {p.Status}" + (p.Reason is { } r ? $" ({r})" : "")));
                throw new InvalidOperationException(
                    $"Cannot stamp stage '{stage.Id}' because its prerequisites are not all fresh: {summary}");
            }
        }
        string? prerequisiteProofHash = CycleStageProofHasher.HashPrerequisites(existing, stage.Prereqs);

        string identity = ChangeSetIdentity.Of(_repositoryRoot, resolvedBaseRef, "HEAD");

        var artifactHashes = new List<string>();
        if (stage.Produces is { } pattern)
        {
            string artifactPath = FreshnessEvaluator.ResolveProduces(pattern, resolvedFeature);
            string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(full))
            {
                artifactHashes.Add(FileHashing.Sha256OfFile(full));
            }
        }

        var proof = new CycleStageProof(
            stage.Id,
            CycleStageOutcome.Stamped,
            identity,
            artifactHashes,
            GitRefs.TryHeadSha(_repositoryRoot),
            prerequisiteProofHash);
        List<CycleStageProof> stages = (existing?.Stages ?? [])
            .Where(s => !string.Equals(s.Stage, stage.Id, StringComparison.OrdinalIgnoreCase))
            .Append(proof)
            .ToList();

        var state = new CycleState(JsonContractDefaults.SchemaVersion, resolvedFeature, resolvedBaseRef, stage.Id, stages);
        _store.Write(state);
        return state;
    }

    public CycleStatusReport Status()
    {
        CycleState state = _store.Read()
            ?? throw new InvalidOperationException(
                $"No cycle state at {CycleStateStore.RelativePath}; run `doti cycle stamp --stage <id> --feature <slug>` first.");

        string identity = ChangeSetIdentity.Of(_repositoryRoot, state.BaseRef, "HEAD");
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);
        List<StageFreshnessResult> freshness = state.Stages
            .Select(proof => evaluator.Evaluate(proof, state.Feature, identity))
            .ToList();

        return new CycleStatusReport(JsonContractDefaults.SchemaVersion, state, freshness);
    }

    /// <summary>Fail-closed chokepoint: every transitive prerequisite of <paramref name="stageId"/> must be
    /// stamped, fresh (artifact + change-set identity unchanged), and valid (a doc prerequisite has no open
    /// [NEEDS CLARIFICATION] marker). The CLI exits non-zero unless <see cref="CycleCheckReport.Passed"/>.</summary>
    public CycleCheckReport Check(string stageId)
    {
        CycleStage target = _stageModel.Find(stageId); // fail-closed on an unknown stage
        CycleState? state = _store.Read();
        string baseRef = state?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot);
        string identity = ChangeSetIdentity.Of(_repositoryRoot, baseRef, "HEAD");
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);

        var results = new List<StagePrereqResult>();
        foreach (string prereqId in ResolveTransitivePrerequisites(target))
        {
            CycleStageProof? proof = state?.Stages.FirstOrDefault(
                s => string.Equals(s.Stage, prereqId, StringComparison.OrdinalIgnoreCase));
            if (proof is null)
            {
                results.Add(new StagePrereqResult(prereqId, "missing", false, "not stamped"));
                continue;
            }

            StageFreshnessResult freshness = evaluator.Evaluate(proof, state!.Feature, identity);
            if (freshness.Freshness == StageFreshness.Stale)
            {
                results.Add(new StagePrereqResult(prereqId, "stale", false, freshness.Reason));
                continue;
            }

            string? openMarker = OpenClarificationMarker(prereqId, state.Feature);
            if (openMarker is not null)
            {
                results.Add(new StagePrereqResult(prereqId, "invalid", false, openMarker));
                continue;
            }

            CycleStage prereqStage = _stageModel.Find(prereqId);
            if (prereqStage.Prereqs.Count > 0)
            {
                string? expectedHash = CycleStageProofHasher.HashPrerequisites(state, prereqStage.Prereqs);
                if (string.IsNullOrWhiteSpace(proof.PrerequisiteProofHash))
                {
                    results.Add(new StagePrereqResult(prereqId, "invalid", false,
                        "missing prerequisite proof hash; re-stamp the stage with the current runner"));
                    continue;
                }

                if (!string.Equals(expectedHash, proof.PrerequisiteProofHash, StringComparison.Ordinal))
                {
                    results.Add(new StagePrereqResult(prereqId, "invalid", false,
                        "prerequisite proof hash differs from the current prerequisite proofs"));
                    continue;
                }
            }

            results.Add(new StagePrereqResult(prereqId, "fresh", true, null));
        }

        bool passed = results.All(r => r.Ok);
        return new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, passed, results);
    }

    /// <summary>The sanctioned commit path. Refuses (no commit) unless <see cref="Check"/> for <c>commit</c>
    /// passes, the persisted gate proof is present + passing + fresh, and the tree is a clean staged scope.
    /// On success, commits the staged tree with <paramref name="message"/> + a <c>Doti-Cycle</c> trailer,
    /// setting the sanctioned-commit sentinel so the insurance hook allows it.</summary>
    public CycleCommitResult Commit(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Refuse("a commit --message is required");
        }

        CycleState? state = _store.Read();
        if (state is null)
        {
            return Refuse($"no cycle state at {CycleStateStore.RelativePath}; stamp the cycle first");
        }

        var reasons = new List<string>();

        CycleCheckReport check = Check("commit");
        if (!check.Passed)
        {
            reasons.AddRange(check.Prerequisites
                .Where(p => !p.Ok)
                .Select(p => $"prerequisite '{p.Stage}': {p.Status} ({p.Reason})"));
        }

        string identity = ChangeSetIdentity.Of(_repositoryRoot, state.BaseRef, "HEAD");
        PersistedGateProof? gateProof = new GateProofStore(_repositoryRoot).Read();
        if (gateProof is null)
        {
            reasons.Add("no gate proof; run `gate run` first");
        }
        else if (gateProof.Proof.Outcome != StageOutcome.Pass)
        {
            reasons.Add($"gate proof is not passing (outcome {gateProof.Proof.Outcome})");
        }
        else if (!string.Equals(gateProof.ChangeSetId, identity, StringComparison.Ordinal))
        {
            reasons.Add("gate proof is stale (the diff changed since the gate ran); re-run `gate run`");
        }
        else
        {
            reasons.AddRange(GateProofValidator.ValidateAffectedTestProof(_repositoryRoot, gateProof));
        }

        CommitScope scope = CommitScopeInspector.Inspect(_repositoryRoot);
        if (!scope.HasStaged)
        {
            reasons.Add("nothing staged to commit");
        }

        if (scope.HasUnstagedTrackedChanges)
        {
            reasons.Add("unstaged tracked changes present; stage or revert them for a deliberate scope");
        }

        if (reasons.Count > 0)
        {
            return new CycleCommitResult(JsonContractDefaults.SchemaVersion, false, null, reasons);
        }

        string fullMessage = $"{message}\n\nDoti-Cycle: {state.Feature}/{state.CurrentStage}";
        ProcessRunResult commit = ProcessRunner.Run(new ToolCommand(
            "git", ["commit", "-m", fullMessage], _repositoryRoot,
            new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" }));
        if (commit.ExitCode != 0)
        {
            return Refuse($"git commit failed: {commit.StandardError.Trim()}");
        }

        return new CycleCommitResult(JsonContractDefaults.SchemaVersion, true, GitRefs.TryHeadSha(_repositoryRoot), []);
    }

    private static CycleCommitResult Refuse(string reason) =>
        new(JsonContractDefaults.SchemaVersion, false, null, [reason]);

    // The transitive prerequisite closure of a stage, returned in workflow declaration order (deterministic).
    private IReadOnlyList<string> ResolveTransitivePrerequisites(CycleStage target)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>(target.Prereqs);
        while (stack.Count > 0)
        {
            string id = stack.Pop();
            if (!seen.Add(id))
            {
                continue;
            }

            foreach (string parent in _stageModel.Find(id).Prereqs)
            {
                stack.Push(parent);
            }
        }

        return _stageModel.Stages.Select(s => s.Id).Where(seen.Contains).ToList();
    }

    // A doc stage's artifact must not still carry an open [NEEDS CLARIFICATION] marker (output discipline).
    private string? OpenClarificationMarker(string stageId, string feature)
    {
        CycleStage stage = _stageModel.Find(stageId);
        if (stage.Produces is not { } pattern)
        {
            return null;
        }

        string artifactPath = FreshnessEvaluator.ResolveProduces(pattern, feature);
        string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(full))
        {
            return null; // absence is already a freshness/missing concern, not an output-validation one
        }

        // A real open marker carries a question — the form `[NEEDS CLARIFICATION: <q>]` (with a
        // colon). A bare, backticked `[NEEDS CLARIFICATION]` is a *mention* of the convention — the
        // scaffold's own doti docs discuss it — so match only the colon form to avoid false positives.
        return File.ReadAllText(full).Contains("[NEEDS CLARIFICATION:", StringComparison.Ordinal)
            ? $"artifact '{artifactPath}' has an open [NEEDS CLARIFICATION:] marker"
            : null;
    }
}
