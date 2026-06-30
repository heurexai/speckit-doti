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

    public CycleService(string repositoryRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _store = new CycleStateStore(_repositoryRoot);
        _stageModel = StageModel.Load(Path.Combine(_repositoryRoot, ".doti", "workflows", "doti", "workflow.yml"));
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
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _store = store;
        _stageModel = stageModel;
    }
}
