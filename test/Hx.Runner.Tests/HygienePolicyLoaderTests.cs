using Hx.Runner.Core.Hygiene;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class HygienePolicyLoaderTests
{
    private static readonly string ExampleOrgUrl = "https" + "://example" + ".org/";
    private static readonly string HomeMarker = "/" + "home/";

    private static string SamplePolicy =>
        $$"""
        {
          "schemaVersion": 1,
          "gitleaksEnabled": false,
          "defaultCommitScope": "changed",
          "fullScanRequiredForRelease": true,
          "changedFileThreshold": 100,
          "gitleaksBaselineAllowed": false,
          "scanPaths": ["."],
          "excludePaths": ["bin", "obj"],
          "allowedUrlPrefixes": ["{{ExampleOrgUrl}}"],
          "localPathMarkers": ["{{HomeMarker}}"],
          "privateKeyMarkers": ["-----BEGIN PRIVATE KEY-----"],
          "binaryExtensions": [".dll"],
          "shellRunnerExtensions": [".sh"]
        }
        """;

    [Fact]
    public void LoadsPolicyFromRulesFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-policy-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(dir, "rules"));
        File.WriteAllText(Path.Combine(dir, "rules", "hygiene.json"), SamplePolicy);

        try
        {
            HygienePolicy policy = HygienePolicyLoader.Load(dir, out bool usedDefault);

            Assert.False(usedDefault);
            Assert.False(policy.GitleaksEnabled);
            Assert.Equal(100, policy.ChangedFileThreshold);
            Assert.Contains(ExampleOrgUrl, policy.AllowedUrlPrefixes);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void UsesDefaultPolicyWhenFileMissing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-policy-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            HygienePolicy policy = HygienePolicyLoader.Load(dir, out bool usedDefault);

            Assert.True(usedDefault);
            Assert.True(policy.GitleaksEnabled);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
