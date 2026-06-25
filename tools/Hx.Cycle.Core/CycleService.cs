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

}
