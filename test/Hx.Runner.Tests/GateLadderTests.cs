using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>007 T006: the repo's declared TIER (not the --profile lane) owns the gate ladder (FR-029/FR-030).</summary>
public sealed class GateLadderTests
{
    private static string NewRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-tier-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteProfile(string repo, string profileName, string profileJson)
    {
        Directory.CreateDirectory(Path.Combine(repo, ".doti"));
        File.WriteAllText(Path.Combine(repo, ".doti", "integration.json"), $"{{\"profile\":\"{profileName}\"}}");
        string profileDir = Path.Combine(repo, ".doti", "profiles", profileName);
        Directory.CreateDirectory(profileDir);
        File.WriteAllText(Path.Combine(profileDir, "profile.json"), profileJson);
    }

    [Fact]
    public void Resolve_WorkflowOnly_SkipsOpinionatedGates_ButDefaultsOthersEnforced()
    {
        string repo = NewRepo();
        try
        {
            WriteProfile(repo, "workflow-only",
                "{\"gates\":{\"sentrux-verify\":\"skip\",\"sentrux-check\":\"skip\",\"architecture-test\":\"skip\"}}");
            GateLadderResolution r = GateLadderResolver.Resolve(repo);
            Assert.True(r.Ok);
            Assert.Equal(GateMode.Skip, r.Ladder!.ModeFor("sentrux-verify"));
            Assert.Equal(GateMode.Skip, r.Ladder.ModeFor("architecture-test"));
            Assert.Equal(GateMode.Enforced, r.Ladder.ModeFor("hygiene")); // undeclared → today's behavior
        }
        finally { Directory.Delete(repo, recursive: true); }
    }

    [Fact]
    public void Resolve_DotnetCliAlias_IsTier3_EnforcesOpinionatedGates()
    {
        string repo = NewRepo();
        try
        {
            WriteProfile(repo, "dotnet-cli",
                "{\"gates\":{\"sentrux-verify\":\"enforced\",\"architecture-test\":\"enforced\"}}");
            GateLadderResolution r = GateLadderResolver.Resolve(repo);
            Assert.True(r.Ok);
            Assert.Equal("dotnet-cli-heurex", r.Ladder!.Tier); // legacy name aliases to the Tier-3 name
            Assert.Equal(GateMode.Enforced, r.Ladder.ModeFor("sentrux-verify"));
        }
        finally { Directory.Delete(repo, recursive: true); }
    }

    [Fact]
    public void Resolve_NoIntegration_DefaultsToNonImposingWorkflowOnly()
    {
        string repo = NewRepo();
        try
        {
            GateLadderResolution r = GateLadderResolver.Resolve(repo);
            Assert.True(r.Ok);
            Assert.Equal(GateLadderResolver.WorkflowOnlyTier, r.Ladder!.Tier);
            Assert.Equal(GateMode.Skip, r.Ladder.ModeFor("sentrux-verify"));
        }
        finally { Directory.Delete(repo, recursive: true); }
    }

    [Fact]
    public void Resolve_DeclaredProfileMissingFile_FailsClosed()
    {
        string repo = NewRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(repo, ".doti"));
            File.WriteAllText(Path.Combine(repo, ".doti", "integration.json"), "{\"profile\":\"dotnet-lib\"}");
            // no .doti/profiles/dotnet-lib/profile.json
            GateLadderResolution r = GateLadderResolver.Resolve(repo);
            Assert.False(r.Ok);
            Assert.Null(r.Ladder);
            Assert.Contains("dotnet-lib", r.FailureReason!);
        }
        finally { Directory.Delete(repo, recursive: true); }
    }

    [Fact]
    public void ValidateLadderCoverage_RefusesADowngradedProof_AcceptsAMatchingOne()
    {
        string repo = NewRepo();
        try
        {
            // Repo declares Tier 3 (dotnet-cli → dotnet-cli-heurex): all opinionated gates enforced.
            WriteProfile(repo, "dotnet-cli",
                "{\"gates\":{\"sentrux-verify\":\"enforced\",\"sentrux-check\":\"enforced\",\"architecture-test\":\"enforced\"}}");

            // A proof minted under a narrowed ladder (architecture skipped) is refused (FR-029).
            var downgraded = new GateProof(1, StageOutcome.Pass, [], [],
                Tier: "dotnet-cli-heurex", LadderCoverage: [new GateLadderEntry("architecture-test", "skip")]);
            IReadOnlyList<string> bad = GateProofValidator.ValidateLadderCoverage(
                repo, new PersistedGateProof(1, "id", "dev", Lane.Normal, downgraded, "sha"));
            Assert.Contains(bad, r => r.Contains("ladder coverage", StringComparison.Ordinal));

            // A proof minted under the declared tier passes.
            GateLadder expected = GateLadderResolver.Resolve(repo).Ladder!;
            var good = new GateProof(1, StageOutcome.Pass, [], [], Tier: expected.Tier, LadderCoverage: expected.Coverage());
            Assert.Empty(GateProofValidator.ValidateLadderCoverage(
                repo, new PersistedGateProof(1, "id", "dev", Lane.Normal, good, "sha")));

            // A pre-FR-029 proof (no tier binding) requires a re-run.
            var legacy = new GateProof(1, StageOutcome.Pass, [], []);
            Assert.Contains(
                GateProofValidator.ValidateLadderCoverage(repo, new PersistedGateProof(1, "id", "dev", Lane.Normal, legacy, "sha")),
                r => r.Contains("predates tier-ladder binding", StringComparison.Ordinal));
        }
        finally { Directory.Delete(repo, recursive: true); }
    }

    [Fact]
    public void Coverage_IsSortedAndRecordsModes()
    {
        var ladder = new GateLadder("dotnet-lib", new Dictionary<string, GateMode>
        {
            ["sentrux-verify"] = GateMode.Skip,
            ["architecture-test"] = GateMode.Advisory,
        });
        IReadOnlyList<Hx.Tooling.Contracts.GateLadderEntry> coverage = ladder.Coverage();
        Assert.Equal(2, coverage.Count);
        Assert.Equal("architecture-test", coverage[0].Step); // sorted ordinally
        Assert.Equal("advisory", coverage[0].Mode);
    }
}
