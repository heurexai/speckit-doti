using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>022 T020 (FR-001/002/004): inspect a repo's Doti version — version+relation for a real Doti repo;
/// not-a-repo when no .doti; version-unknown when .doti exists but no payload.</summary>
public sealed class DotiVersionInspectorTests
{
    [Fact]
    public void Doti_repo_reports_version_and_relation()
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        try
        {
            RepoPayloadStore.Write(repo, "0.13.5", "0.13.5");

            DotiRepoVersion v = DotiVersionInspector.Inspect(repo, "0.13.5");

            Assert.Equal(DotiVersionStatus.Ok, v.Status);
            Assert.Equal("0.13.5", v.PayloadVersion);
            Assert.Equal(DotiVersionRelation.Current, v.Relation);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(repo);
        }
    }

    [Fact]
    public void Outdated_repo_relation_is_outdated()
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        try
        {
            RepoPayloadStore.Write(repo, "0.13.2", "0.13.2");

            DotiRepoVersion v = DotiVersionInspector.Inspect(repo, "0.13.5");

            Assert.Equal(DotiVersionStatus.Ok, v.Status);
            Assert.Equal(DotiVersionRelation.Outdated, v.Relation);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(repo);
        }
    }

    [Fact]
    public void No_doti_directory_is_not_a_repo()
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiRepoVersion v = DotiVersionInspector.Inspect(repo, "0.13.5");

            Assert.Equal(DotiVersionStatus.NotARepo, v.Status);
            Assert.Equal(DotiVersionRelation.Unknown, v.Relation);
            Assert.Null(v.PayloadVersion);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(repo);
        }
    }

    [Fact]
    public void Doti_without_payload_is_version_unknown()
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(repo, ".doti"));

            DotiRepoVersion v = DotiVersionInspector.Inspect(repo, "0.13.5");

            Assert.Equal(DotiVersionStatus.VersionUnknown, v.Status);
            Assert.Equal(DotiVersionRelation.Unknown, v.Relation);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(repo);
        }
    }
}
