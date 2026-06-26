using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T025: the Store-release workflow builds the MSIX from CURATED, source-free staging (no <c>git archive</c>,
/// no source tree, tool binaries fetched on demand per T022), and is hardened symmetrically with the NuGet
/// publish — a two-job split with Store credentials isolated to the <c>submit</c> job behind the protected
/// <c>store-release</c> Environment, and the unsigned MSIX kept a Store-submission input (never a GitHub release
/// asset). Structural assertion — the workflow itself only runs in GitHub Actions.
/// </summary>
public sealed class StoreReleaseWorkflowTests
{
    private static string Workflow() =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "store-release.yml"));

    [Fact]
    public void Store_workflow_stages_the_source_free_payload_not_a_git_archive_of_source()
    {
        string workflow = Workflow();

        // Curated staging of the source-free payload, with the descriptor stamped for the msix channel.
        Assert.Contains("dotnet pack scaffold/Hx.Scaffold.Templates.csproj", workflow);
        Assert.Contains("doti payload-manifest", workflow);
        Assert.Contains("--channel msix", workflow);
        Assert.Contains("makeappx.exe", workflow);

        // No source tree in the package: the old `git archive --format=tar HEAD | tar -x` staging command is
        // gone (no-source gate). The header comment may still NAME it to document the removal.
        Assert.DoesNotContain("git archive --format=tar", workflow);
        // The per-RID tool binaries are fetched on demand (T022), never bundled — the bin/ is stripped from each
        // staged tool dir and there is no pre-fetch step.
        Assert.Contains("Remove-Item -Recurse -Force \"$layout/tools/$t/bin\"", workflow);
        Assert.DoesNotContain("tools fetch", workflow);
        // Velopack/vpk never participated in the Store path; keep it that way.
        Assert.DoesNotContain("vpk", workflow);
    }

    [Fact]
    public void Store_workflow_isolates_store_credentials_behind_a_reviewed_environment()
    {
        string workflow = Workflow();

        // Two-job split: pack (no Store secrets) and submit (the only holder of Store credentials).
        Assert.Contains("needs: pack", workflow);
        Assert.Contains("environment: store-release", workflow);

        // Store credentials appear exactly once each — only in the submit job, never in the build/pack job.
        Assert.Equal(1, Occurrences(workflow, "secrets.STORE_CLIENT_SECRET"));
        Assert.Equal(1, Occurrences(workflow, "secrets.STORE_TENANT_ID"));

        // Minimal permissions, no write anywhere — the unsigned MSIX is never attached to a GitHub release.
        Assert.Contains("contents: read", workflow);
        Assert.DoesNotContain("contents: write", workflow);
        Assert.DoesNotContain("gh release", workflow);

        // H10: action-SHA-pinning is documented as required operator prep.
        Assert.Contains("pin to a full commit SHA", workflow);
    }

    private static int Occurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }

        return count;
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
