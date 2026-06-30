using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T019: assert <see cref="SetupConfigDefaults"/> equals the values documented in
/// <c>docs/configuration.md</c> (the spec's stated source of truth for the defaults). A drift between the code
/// defaults and the doc fails this test, keeping the single-source promise honest.</summary>
public sealed class SetupConfigDefaultsParityTests
{
    [Fact]
    public void Documented_defaults_match_the_code_defaults()
    {
        string doc = File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", "configuration.md"));

        // identity
        Assert.Equal("Heurex", SetupConfigDefaults.Company);
        Assert.Contains("| `company` |", doc);
        Assert.Contains("| `Heurex` |", doc);

        Assert.Equal("MIT", SetupConfigDefaults.License);
        Assert.Contains("`MIT`", doc);

        // versioning
        Assert.Equal("0.1.0", SetupConfigDefaults.NextVersion);
        Assert.Contains("`0.1.0`", doc);

        // release
        Assert.Equal("DOTI_RELEASE_ROOT", SetupConfigDefaults.ReleaseEnvironmentVariable);
        Assert.Contains("DOTI_RELEASE_ROOT", doc);
        Assert.True(SetupConfigDefaults.ReleaseEnabled);
        Assert.Contains("`localReleaseOutput.enabled`", doc);

        // publish
        Assert.Equal("release.yml", SetupConfigDefaults.PublishWorkflow);
        Assert.Contains("`release.yml`", doc);
        Assert.Equal("production", SetupConfigDefaults.PublishEnvironment);
        Assert.Contains("`production`", doc);

        // agents
        Assert.Equal("codex,claude", SetupConfigDefaults.Agents);
        Assert.Contains("`codex,claude`", doc);

        // constitution §2 placeholders
        Assert.Equal("[DOMAIN_PRINCIPLES]", SetupConfigDefaults.DomainPrinciples);
        Assert.Contains("[DOMAIN_PRINCIPLES]", doc);
        Assert.Equal("[TECH_STACK]", SetupConfigDefaults.TechStack);
        Assert.Contains("[TECH_STACK]", doc);
    }

    [Fact]
    public void Every_registered_key_default_matches_its_descriptor()
    {
        // The registry is the single key source — assert each key's Default constant lines up with SetupConfigDefaults.
        Assert.Equal(SetupConfigDefaults.License, SetupKeys.ById_(SetupKeys.IdentityLicense).Default);
        Assert.Equal(SetupConfigDefaults.NextVersion, SetupKeys.ById_(SetupKeys.VersioningNextVersion).Default);
        Assert.Equal(SetupConfigDefaults.ReleaseEnvironmentVariable, SetupKeys.ById_(SetupKeys.ReleaseEnvironmentVariable).Default);
        Assert.Equal(SetupConfigDefaults.Agents, SetupKeys.ById_(SetupKeys.Agents).Default);
        Assert.Equal(SetupConfigDefaults.PublishEnvironment, SetupKeys.ById_(SetupKeys.PublishEnvironment).Default);
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "scaffold-dotnet.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir)!;
        }

        throw new InvalidOperationException("Could not locate the repo root (scaffold-dotnet.slnx).");
    }
}
