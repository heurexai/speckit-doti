using Hx.Cycle.Core;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 022 (Bug#2): the proof base ref is single-sourced and the active cycle base is authoritative. Previously the gate
/// planned its affected-test set off <c>dev</c> while the proof persistence recorded the cycle base — once the cycle
/// base advanced (rebase-to-head per transition) the two diverged and the diff/release transition rejected an
/// otherwise-valid proof. <see cref="GitRefs.PickProofBaseRef"/> is the pure priority pick both paths share.
/// </summary>
public sealed class GitRefsProofBaseRefTests
{
    [Fact]
    public void Cycle_base_wins_over_dev_and_head()
    {
        // The exact bug: a cycle is active (base advanced to the implement commit) AND a dev branch exists at a
        // different commit. The cycle base must win so the gate and the proof agree.
        Assert.Equal("cycle-sha", GitRefs.PickProofBaseRef("cycle-sha", "dev-sha", "head-sha"));
    }

    [Fact]
    public void Falls_back_to_dev_when_no_cycle()
    {
        Assert.Equal("dev-sha", GitRefs.PickProofBaseRef(null, "dev-sha", "head-sha"));
        Assert.Equal("dev-sha", GitRefs.PickProofBaseRef("   ", "dev-sha", "head-sha"));
    }

    [Fact]
    public void Falls_back_to_head_when_no_cycle_and_no_dev()
    {
        Assert.Equal("head-sha", GitRefs.PickProofBaseRef(null, null, "head-sha"));
    }

    [Fact]
    public void Falls_back_to_symbolic_head_when_nothing_resolves()
    {
        Assert.Equal("HEAD", GitRefs.PickProofBaseRef(null, null, null));
    }

    [Fact]
    public void Resolved_values_are_trimmed()
    {
        Assert.Equal("cycle-sha", GitRefs.PickProofBaseRef("  cycle-sha\n", "dev-sha", "head-sha"));
    }
}
