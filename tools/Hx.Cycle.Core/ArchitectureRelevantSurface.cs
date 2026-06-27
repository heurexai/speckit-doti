using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// FR-027: does a change touch an ARCHITECTURE-RELEVANT surface — contracts, CLI shape, dependency direction /
/// layering, persistence, security, generated-code templates, or cross-module structure? This is the predicate the
/// arch-review skill (and implement / drift-patching) checks to decide whether arch-review must RE-RUN: a docs /
/// Doti-prose-only change does not touch architecture and need not re-run the design lenses. Reuses the
/// review-context categorisation (one taxonomy, two read sites) so the predicate can never disagree with the lens
/// projection, and fails closed — an unresolved layer map ⇒ relevant.
/// </summary>
public sealed class ArchitectureRelevantSurface
{
    private readonly ReviewContextProjector _projector;

    public ArchitectureRelevantSurface(LayerMap layers) => _projector = new ReviewContextProjector(layers);

    public bool IsTouched(ChangeSetContext changeSet)
    {
        ReviewContext context = _projector.Project(changeSet);
        // Architecture-relevant unless the change is purely docs/prose AND nothing escalated (broad/unattributed
        // input, or a fail-closed unresolved layer map).
        return !context.IsDocsOnly || context.EscalationReasons.Count > 0;
    }
}
