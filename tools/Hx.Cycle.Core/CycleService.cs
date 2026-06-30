using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// Records + reports cycle state (<see cref="Stamp"/>/<see cref="Status"/> — non-enforcing) and
/// <b>enforces</b> it: <see cref="Check"/> is the fail-closed prerequisite chokepoint, while
/// <see cref="Stamp"/> creates sanctioned automatic transition commits before a next stage starts.
/// Fails closed (throws) only on a genuine execution error; otherwise returns a verdict the CLI maps to
/// an exit code.
/// </summary>
public sealed partial class CycleService
{
    private readonly string _repositoryRoot;
    private readonly CycleStateStore _store;
    private readonly StageModel _stageModel;

    // 030 (bug-release-bridge): release-ready bug mini-cycle members the release train ALSO carries (a bug-fix-only
    // repo must release). The bug records live under Hx.Doti.Core (.doti/bugs/<id>/), which depends ON Hx.Cycle.Core —
    // so the enumeration is INJECTED as a delegate here (NEVER a Cycle→Doti reference; arch-rule cycleCoreNoDotiCore).
    // The default is empty: a caller that does not wire the bug bridge sees exactly the prior feature-only train.
    private readonly Func<string, IReadOnlyList<CycleReleaseTrainFeature>> _bugReleaseMembers;

    public CycleService(string repositoryRoot)
        : this(repositoryRoot, bugReleaseMembers: null)
    {
    }

    /// <summary>
    /// 030 (bug-release-bridge): construct with the bug-release-member provider wired in. The Doti-aware callers (the
    /// local release service, the release-lane gate, and the cycle CLI) pass <c>BugCycleService.ReleaseReadyBugMembers</c>
    /// so the release train bridges a test-passed bug mini-cycle; every other (test/non-release) caller keeps the
    /// feature-only default. Kept as an injected delegate so Hx.Cycle.Core never gains a forbidden edge to Hx.Doti.Core.
    /// </summary>
    public CycleService(string repositoryRoot, Func<string, IReadOnlyList<CycleReleaseTrainFeature>>? bugReleaseMembers)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _store = new CycleStateStore(_repositoryRoot);
        _stageModel = StageModel.Load(Path.Combine(_repositoryRoot, ".doti", "workflows", "doti", "workflow.yml"));
        _bugReleaseMembers = bugReleaseMembers ?? (_ => []);
    }

    /// <summary>028 FR-010: the loaded stage model — the single source the CLI builds the <c>DotiActionModel</c>/
    /// <c>DotiActionProjector</c> over when mapping workflow next-actions through <c>CliActionRendering</c>.</summary>
    public StageModel StageModel => _stageModel;

    /// <summary>
    /// 028 H5: the test seam. Injects the <see cref="CycleStateStore"/> + <see cref="StageModel"/> so the
    /// <see cref="ReviewRebind"/> verb and its one-write atomicity (SC-007) are exercisable without re-deriving the
    /// store/model from a real repo. The git-dependent paths (change-set identity, HEAD sha) still resolve from
    /// <paramref name="repositoryRoot"/>; an attestable (non-change-set-bound) stage's freshness is file-content-bound,
    /// so the verb is testable git-free. Internal — production code uses the public ctor.
    /// </summary>
    internal CycleService(string repositoryRoot, CycleStateStore store, StageModel stageModel)
        : this(repositoryRoot, store, stageModel, bugReleaseMembers: null)
    {
    }

    /// <summary>028 H5 test seam, 030: also accepts the bug-release-member provider so the bug-bridge train can be
    /// exercised git-free against an injected store/model.</summary>
    internal CycleService(
        string repositoryRoot,
        CycleStateStore store,
        StageModel stageModel,
        Func<string, IReadOnlyList<CycleReleaseTrainFeature>>? bugReleaseMembers)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _store = store;
        _stageModel = stageModel;
        _bugReleaseMembers = bugReleaseMembers ?? (_ => []);
    }
}
