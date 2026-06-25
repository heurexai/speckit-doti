using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// Heavy end-to-end smoke for <c>Hx.Scaffold.Cli new</c> (gated behind <c>HX_SCAFFOLD_SMOKE=1</c> so it
/// is not on the inner loop). Generates a repo from the template, finishes it (vendor tooling + Doti),
/// runs the first smoke, and asserts a Pass proof plus the self-hosting file shape. Fully green only on
/// win-x64 (Sentrux/Gitleaks are vendored for win-x64; other RIDs are Blocked / fail closed).
/// The nested dotnet calls inside <see cref="ScaffoldNewRunner"/> use the round-trip hang fix
/// (concurrent pipe drain + build-server isolation), so this is safe to run under the test host.
/// </summary>
public sealed class ScaffoldNewSmokeTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("HX_SCAFFOLD_SMOKE") == "1";

    [Fact]
    public void New_generates_finishes_and_smokes_a_self_hosting_repo()
    {
        Assert.SkipUnless(Enabled, "Set HX_SCAFFOLD_SMOKE=1 to run the heavy scaffold-new end-to-end smoke.");

        string sandbox = Path.Combine(Path.GetTempPath(), "hx-new-smoke-" + Guid.NewGuid().ToString("n"));
        string output = Path.Combine(sandbox, "Hx.NewSmoke.Sample");
        try
        {
            string sourceRoot = ScaffoldRoot.Find(AppContext.BaseDirectory);
            var request = new ScaffoldRequest("Hx.NewSmoke.Sample", "Heurex", output, "dotnet-cli", ["codex", "claude"]);

            ScaffoldProof proof = ScaffoldNewRunner.Run(request, sourceRoot);

            Assert.Equal(StageOutcome.Pass, proof.Template.Outcome);
            Assert.Equal(StageOutcome.Pass, proof.Smoke.Outcome);
            Assert.Equal(StageOutcome.Pass, proof.Outcome);

            // Self-hosting shape: .doti source (so installed skills' .doti/core refs resolve), rendered
            // skills, vendored runner + tools, and the first-smoke baseline.
            Assert.True(File.Exists(Path.Combine(output, ".doti", "core", "skills.json")), ".doti source installed");
            Assert.True(File.Exists(Path.Combine(output, ".doti", "core", "templates", "commands", "doti-specify.md")),
                "command templates installed (skills' .doti/core references resolve)");
            Assert.True(File.Exists(Path.Combine(output, ".claude", "skills", "09-doti-release", "SKILL.md")),
                ".claude numbered release skill rendered");
            Assert.True(File.Exists(Path.Combine(output, ".agents", "skills", "01-doti-specify", "SKILL.md")),
                ".agents numbered specify skill rendered");
            Assert.Contains("Run `/09-doti-release` to release, or `/01-doti-specify`",
                File.ReadAllText(Path.Combine(output, ".agents", "skills", "08-doti-drift-review", "SKILL.md")));
            Assert.True(File.Exists(Path.Combine(output, "AGENTS.md")) && File.Exists(Path.Combine(output, "CLAUDE.md")),
                "root entrypoints rendered");
            Assert.True(File.Exists(Path.Combine(output, ".doti", "integration.json")), "repo-specific Doti metadata");
            Assert.True(File.Exists(Path.Combine(output, "tools", "Hx.Runner.Cli", "Hx.Runner.Cli.csproj")),
                "runner source vendored");
            Assert.True(File.Exists(Path.Combine(output, "tools", "gitleaks", "gitleaks.version.json")),
                "Gitleaks vendored with manifest");
            Assert.True(File.Exists(Path.Combine(output, "tools", "sentrux", "sentrux.version.json")),
                "Sentrux vendored with manifest");
            Assert.True(File.Exists(Path.Combine(output, ".sentrux", "baseline.json")), "first-smoke baseline created");
        }
        finally
        {
            TryDelete(sandbox);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort sandbox cleanup
        }
    }
}
