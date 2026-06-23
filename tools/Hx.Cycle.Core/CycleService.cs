using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// Records + reports cycle state (<see cref="Stamp"/>/<see cref="Status"/> — non-enforcing) and
/// <b>enforces</b> it: <see cref="Check"/> is the fail-closed chokepoint (every transitive
/// prerequisite stamped + fresh + valid) and <see cref="Commit"/> is the sanctioned commit path (refuses
/// unless the prerequisites, the persisted gate proof, and the staged scope are all clean). Fails closed
/// (throws) only on a genuine execution error; otherwise returns a verdict the CLI maps to an exit code.
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

}
