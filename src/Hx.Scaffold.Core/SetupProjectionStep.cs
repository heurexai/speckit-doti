using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Scaffold.Core;

/// <summary>
/// 029 FR-002/FR-003/FR-006/D3/D10: the post-template setup-config PROJECTION step for <c>hx new</c>, factored out of
/// <see cref="ScaffoldNewRunner"/> so the orchestrator does not itself reference the projector + the per-asset writers
/// + the intent store. Lives in <c>Scaffold.Core</c> (which may depend on <c>Doti.Core</c>), so it introduces no
/// <c>Doti.Core→Scaffold.Core</c> edge. <b>D10 no-op fence:</b> a null <paramref name="setup"/> early-returns inside
/// <see cref="SetupConfigProjector.Project"/> before any write, and the intent store is skipped — SC-007 byte-identical.
/// </summary>
public static class SetupProjectionStep
{
    /// <summary>Project the resolved operator setup config into the just-generated repo (the .csproj metadata,
    /// GitVersion seed, release env-var, constitution §2) and persist the repo-portable intent to <c>.doti/setup.json</c>.
    /// Runs AFTER doti-install so the constitution + release.json exist with their template defaults.</summary>
    public static void Apply(ResolvedSetupConfig? setup, string targetRoot, string projectName)
    {
        SetupConfigProjector.Project(setup, targetRoot, SetupTargetWriters.ForNew(projectName));
        if (setup is not null)
        {
            // FR-003: persist the operator INTENT (repo-portable only; machine-local fields stripped, D6) to the
            // tracked .doti/setup.json so re-runs/upgrades read the same intent. Null Setup writes nothing.
            SetupConfigStore.WriteFromResolved(targetRoot, setup);
        }
    }
}
