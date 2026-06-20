using Hx.Runner.Core.Hygiene;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class ScaffoldHygieneChecksTests
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");
    private static string PrivateKeyBeginMarker => "-----BEGIN " + "RSA PRIVATE KEY-----";
    private static string PrivateKeyEndMarker => "-----END " + "RSA PRIVATE KEY-----";

    private static ScanFile Fix(string name) => new(name, Path.Combine(FixturesDir, name));

    [Fact]
    public void CleanFileHasNoFindings()
    {
        IReadOnlyList<HygieneFinding> findings = ScaffoldHygieneChecks.Scan(HygienePolicy.Default(), [Fix("clean.txt")]);

        Assert.Empty(findings);
    }

    [Fact]
    public void DetectsDeveloperLocalPath()
    {
        IReadOnlyList<HygieneFinding> findings = ScaffoldHygieneChecks.Scan(HygienePolicy.Default(), [Fix("local-path.txt")]);

        Assert.Contains(findings, f =>
            f.Category == HygieneFindingCategory.LocalPath && f.Severity == HygieneSeverity.Error);
    }

    [Fact]
    public void DetectsPrivateKeyBlock()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-private-key-fixture-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "private-key.pem");
        File.WriteAllText(
            path,
            PrivateKeyBeginMarker + Environment.NewLine
            + "not-a-real-key" + Environment.NewLine
            + PrivateKeyEndMarker + Environment.NewLine);

        try
        {
            IReadOnlyList<HygieneFinding> findings = ScaffoldHygieneChecks.Scan(
                HygienePolicy.Default(),
                [new ScanFile("private-key.pem", path)]);

            Assert.Contains(findings, f =>
                f.Category == HygieneFindingCategory.PrivateKey && f.Severity == HygieneSeverity.Error);
            Assert.DoesNotContain(findings, f => f.Description.Contains("not-a-real-key", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ExternalUrlIsWarningNotError()
    {
        IReadOnlyList<HygieneFinding> findings = ScaffoldHygieneChecks.Scan(HygienePolicy.Default(), [Fix("external-url.md")]);

        Assert.Contains(findings, f => f.Category == HygieneFindingCategory.ExternalUrl);
        Assert.All(findings, f => Assert.Equal(HygieneSeverity.Warning, f.Severity));
    }

    [Fact]
    public void DetectsGeneratedShellRunnerByExtension()
    {
        IReadOnlyList<HygieneFinding> findings = ScaffoldHygieneChecks.Scan(HygienePolicy.Default(), [Fix("runner.sh")]);

        Assert.Contains(findings, f =>
            f.Category == HygieneFindingCategory.ShellRunner && f.Severity == HygieneSeverity.Error);
    }
}
