using Hx.Sentrux.Core;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxPolicyLoaderTests
{
    private const string Sample =
        """
        {
          "schemaVersion": 1,
          "sentruxEnabled": false,
          "baselinePath": ".sentrux/baseline.json",
          "rulesConfigPath": ".sentrux/rules.toml",
          "signalToleranceBand": 250,
          "forkStamp": "Heurex fork",
          "firstSmokeBaseline": true,
          "requiredFeatures": ["check-include-untracked"],
          "requiredGrammars": ["csharp"]
        }
        """;

    [Fact]
    public void LoadsPolicyFromRulesFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-sxp-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(dir, "rules"));
        File.WriteAllText(Path.Combine(dir, "rules", "sentrux.json"), Sample);

        try
        {
            SentruxPolicy policy = SentruxPolicyLoader.Load(dir, out bool usedDefault);

            Assert.False(usedDefault);
            Assert.False(policy.SentruxEnabled);
            Assert.Equal(250, policy.SignalToleranceBand);
            Assert.Equal("Heurex fork", policy.ForkStamp);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void UsesDefaultPolicyWhenMissing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-sxp-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            SentruxPolicy policy = SentruxPolicyLoader.Load(dir, out bool usedDefault);

            Assert.True(usedDefault);
            Assert.True(policy.SentruxEnabled);
            Assert.Equal(".sentrux/baseline.json", policy.BaselinePath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
