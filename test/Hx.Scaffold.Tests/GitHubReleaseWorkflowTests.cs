using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T024: the release workflow publishes the framework-dependent global tool <c>Heurex.SpeckitDoti</c> to
/// NuGet.org via Trusted Publishing (GitHub OIDC — no stored key), runs the source-free install smoke before
/// publishing, and is H10-hardened (OIDC on the publish job only, behind the protected <c>production</c> Environment).
/// Velopack/vpk packaging is gone. Structural assertion — the workflow itself only runs in GitHub Actions.
/// </summary>
public sealed class GitHubReleaseWorkflowTests
{
    private static string Workflow() =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "release.yml"));

    [Fact]
    public void Release_workflow_publishes_the_global_tool_via_trusted_publishing_not_velopack()
    {
        string workflow = Workflow();

        // Pack + push the framework-dependent global tool to public NuGet.org. FR-003: pack via the two-phase
        // PackAnchoredTool target (embeds the payload-manifest anchor), never a plain `dotnet pack`.
        Assert.Contains("build tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj -c Release -t:PackAnchoredTool", workflow);
        Assert.Contains("dotnet nuget push", workflow);
        Assert.Contains("https://api.nuget.org/v3/index.json", workflow);

        // The source-free install smoke runs before publish: install the packed tool, exercise the documented path.
        Assert.Contains("dotnet tool install Heurex.SpeckitDoti", workflow);
        Assert.Contains("hx new", workflow);
        Assert.Contains("hx doti install", workflow);

        // Velopack/vpk packaging is gone (T016/T017); no source-archive release.
        Assert.DoesNotContain("vpk pack", workflow);
        Assert.DoesNotContain("vpk.dll", workflow);
        Assert.DoesNotContain("vpk-payload", workflow);
        Assert.DoesNotContain("releaseProduct = \"velopack\"", workflow);
        Assert.DoesNotContain("git archive", workflow);
        Assert.DoesNotContain("Compress-Archive", workflow);
    }

    [Fact]
    public void Release_workflow_uses_oidc_trusted_publishing_with_no_stored_key_and_h10_hardening()
    {
        string workflow = Workflow();

        // Trusted Publishing: the API key is OIDC-derived (NuGet/login), never a stored secret.
        Assert.Contains("NuGet/login@", workflow);
        Assert.Contains("steps.login.outputs.NUGET_API_KEY", workflow);
        Assert.DoesNotContain("secrets.NUGET_API_KEY", workflow);

        // H10: OIDC on the publish job only; the workflow declares no top-level write permission.
        Assert.Contains("id-token: write", workflow);
        Assert.DoesNotContain("contents: write", workflow);

        // H10: publish behind the protected `production` Environment — its name matches the NuGet Trusted
        // Publishing policy's Environment field; a required reviewer gates OIDC issuance.
        Assert.Contains("environment: production", workflow);

        // H10: every action is pinned to a full 40-char commit SHA — a pin is present, no floating @vN tag remains.
        Assert.Matches(@"uses:\s*[^\s@]+@[0-9a-f]{40}", workflow);
        Assert.DoesNotMatch(@"uses:\s*[^\s@]+@v\d", workflow);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output.");
    }
}
