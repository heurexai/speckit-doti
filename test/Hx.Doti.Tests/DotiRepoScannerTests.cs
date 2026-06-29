using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>022 T030 (FR-005/006/007): discover exactly the Doti repos under a root, skip non-Doti + pruned dirs,
/// don't descend into a found repo, empty→empty, malformed payload→unknown.</summary>
public sealed class DotiRepoScannerTests
{
    [Fact]
    public void Discovers_exactly_the_doti_repos_under_root()
    {
        string root = DotiVersionTestSupport.NewTempDir();
        try
        {
            StampRepo(Path.Combine(root, "a"), "0.13.5");
            StampRepo(Path.Combine(root, "nested", "b"), "0.13.2");
            Directory.CreateDirectory(Path.Combine(root, "plain")); // not a Doti repo

            DotiScanResult result = DotiRepoScanner.Scan(root, "0.13.5");

            Assert.Equal(2, result.Count);
            Assert.Contains(result.Repos, r => r.PayloadVersion == "0.13.5" && r.Relation == DotiVersionRelation.Current);
            Assert.Contains(result.Repos, r => r.PayloadVersion == "0.13.2" && r.Relation == DotiVersionRelation.Outdated);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(root);
        }
    }

    [Fact]
    public void Does_not_descend_into_a_discovered_repo()
    {
        string root = DotiVersionTestSupport.NewTempDir();
        try
        {
            string outer = Path.Combine(root, "outer");
            StampRepo(outer, "0.13.5");
            StampRepo(Path.Combine(outer, "inner"), "0.13.2"); // nested inside a discovered repo — must be ignored

            DotiScanResult result = DotiRepoScanner.Scan(root, "0.13.5");

            Assert.Equal(1, result.Count);
            Assert.Equal("0.13.5", result.Repos[0].PayloadVersion);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(root);
        }
    }

    [Fact]
    public void Empty_tree_is_explicit_empty_success()
    {
        string root = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiScanResult result = DotiRepoScanner.Scan(root, "0.13.5");
            Assert.Equal(0, result.Count);
            Assert.Empty(result.Repos);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(root);
        }
    }

    [Fact]
    public void Malformed_payload_is_discovered_as_unknown()
    {
        string root = DotiVersionTestSupport.NewTempDir();
        try
        {
            string repo = Path.Combine(root, "broken");
            Directory.CreateDirectory(Path.Combine(repo, ".doti"));
            File.WriteAllText(Path.Combine(repo, ".doti", "payload.json"), "{ broken ");

            DotiScanResult result = DotiRepoScanner.Scan(root, "0.13.5");

            Assert.Equal(1, result.Count);
            Assert.Equal(DotiVersionStatus.VersionUnknown, result.Repos[0].Status);
            Assert.Equal(DotiVersionRelation.Unknown, result.Repos[0].Relation);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(root);
        }
    }

    private static void StampRepo(string repo, string version)
    {
        Directory.CreateDirectory(repo);
        RepoPayloadStore.Write(repo, version, version);
    }
}
