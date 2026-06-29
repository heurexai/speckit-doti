using Hx.Doti.Core;
using Hx.Scaffold.Core.Versioning;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 022 T023 (FR-003): <c>hx version --repo</c> sources <c>targetRelation</c> from <c>.doti/payload.json</c>, not the
/// (empty/absent) scaffold stamp — so a clean doti-adopted repo reads <c>equal</c>, NOT the spurious <c>newer</c>.
/// The scaffold-version stamp is the fallback only when payload.json is absent.
/// </summary>
public sealed class VersionTargetRelationTests
{
    [Fact]
    public void Clean_adopted_repo_reads_equal_from_payload_not_newer()
    {
        string repo = NewTempDir();
        try
        {
            RepoPayloadStore.Write(repo, "0.13.5", "0.13.5");

            ScaffoldVersionReport report = ScaffoldVersionReporter.Report("0.13.5", repo);

            Assert.Equal(ScaffoldVersionRelation.Equal, report.TargetRelation);
            Assert.NotNull(report.Target);
            Assert.Equal("0.13.5", report.Target!.Version);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Outdated_repo_reads_newer_running_tool()
    {
        string repo = NewTempDir();
        try
        {
            RepoPayloadStore.Write(repo, "0.13.2", "0.13.2");

            ScaffoldVersionReport report = ScaffoldVersionReporter.Report("0.13.5", repo);

            Assert.Equal(ScaffoldVersionRelation.Newer, report.TargetRelation);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Ahead_repo_reads_behind_running_tool()
    {
        string repo = NewTempDir();
        try
        {
            RepoPayloadStore.Write(repo, "0.14.0", "0.14.0");

            ScaffoldVersionReport report = ScaffoldVersionReporter.Report("0.13.5", repo);

            Assert.Equal(ScaffoldVersionRelation.Behind, report.TargetRelation);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Falls_back_to_scaffold_stamp_when_payload_absent()
    {
        string repo = NewTempDir();
        try
        {
            ScaffoldVersionReporter.WriteStamp(repo, ScaffoldVersionReporter.IdentityFromVersion("0.13.5", "hx-scaffold"));

            ScaffoldVersionReport report = ScaffoldVersionReporter.Report("0.13.5", repo);

            Assert.Equal(ScaffoldVersionRelation.Equal, report.TargetRelation);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Unknown_when_neither_payload_nor_scaffold_stamp_present()
    {
        string repo = NewTempDir();
        try
        {
            ScaffoldVersionReport report = ScaffoldVersionReporter.Report("0.13.5", repo);
            Assert.Equal(ScaffoldVersionRelation.Unknown, report.TargetRelation);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-ver-rel-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
