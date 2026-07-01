using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Git;

/// <summary>
/// 039 WI2: an ordered compensation ledger that makes a coded operation (here, <c>hx release</c>) all-or-nothing —
/// the engine-owned code rollback the operator asked for. Each durable side effect is <see cref="Record"/>ed with its
/// undo; on any failure <see cref="Rollback"/> runs the undos in reverse, BEST-EFFORT-ALL (a failing compensation does
/// not skip the rest), and returns a fail-closed <see cref="RollbackReport"/> that flags residual leftovers; on success
/// <see cref="Commit"/> keeps the durable results. Deliberately NOT a whole-repository snapshot: it compensates only the
/// operation's OWN recorded effects, so a concurrent operator edit in the working tree is never touched (the arch-review
/// BLOCKER-3 correction).
/// </summary>
public sealed class ReleaseTransaction
{
    private readonly List<(string Action, Action Undo, Action? Cleanup)> _ledger = [];

    /// <summary>Record a durable side effect, how to undo it (run in reverse order on <see cref="Rollback"/>), and an
    /// optional <paramref name="cleanup"/> run on <see cref="Commit"/> (e.g. delete a baseline backup no longer needed).</summary>
    public void Record(string action, Action undo, Action? cleanup = null) => _ledger.Add((action, undo, cleanup));

    /// <summary>Undo every recorded side effect (reverse order, best-effort-all) and report the outcome. Never throws —
    /// a compensation failure becomes a non-succeeded <see cref="CompensationOutcome"/> (a residual), not an exception.</summary>
    public RollbackReport Rollback(ReleaseStage failedStage, string reason)
    {
        var outcomes = new List<CompensationOutcome>(_ledger.Count);
        for (int i = _ledger.Count - 1; i >= 0; i--)
        {
            (string action, Action undo, _) = _ledger[i];
            try
            {
                undo();
                outcomes.Add(new CompensationOutcome(action, true, null));
            }
            catch (Exception ex)
            {
                outcomes.Add(new CompensationOutcome(action, false, ex.Message));
            }
        }

        _ledger.Clear();
        return new RollbackReport(failedStage, reason, outcomes);
    }

    /// <summary>Keep the recorded side effects (the release succeeded); run each cleanup best-effort, then drop the ledger.</summary>
    public void Commit()
    {
        foreach ((_, _, Action? cleanup) in _ledger)
        {
            try { cleanup?.Invoke(); }
            catch { /* best-effort: a leftover baseline backup is a leak, not a correctness failure */ }
        }

        _ledger.Clear();
    }
}
