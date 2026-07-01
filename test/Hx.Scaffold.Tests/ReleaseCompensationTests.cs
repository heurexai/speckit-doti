using Hx.Scaffold.Core.Release;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>039 WI2/SC-002/SC-003: the release's compensations restore their own side effects EXACTLY — proven
/// deterministically (no dotnet/network) by exercising the capture/restore helpers the ledger records.</summary>
public sealed class ReleaseCompensationTests
{
    [Fact]
    public void RestoreDirBaseline_removesADirTheRunCreated_whenNoneExistedBefore()
    {
        string root = NewTempDir();
        try
        {
            string dir = Path.Combine(root, "0.1.2");
            LocalReleaseService.DirBaseline baseline = LocalReleaseService.CaptureDirBaseline(dir); // absent baseline
            Directory.CreateDirectory(dir);                                                          // the run "creates" it
            File.WriteAllText(Path.Combine(dir, "ergon.exe"), "artifact");

            LocalReleaseService.RestoreDirBaseline(dir, baseline);

            Assert.False(Directory.Exists(dir), "a release-root dir the run created must be removed on rollback (the ergon-v0.1.2 leftover)");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void RestoreDirBaseline_restoresAPreExistingDir_andDropsTheHalfReleasedContent()
    {
        string root = NewTempDir();
        try
        {
            string dir = Path.Combine(root, "0.1.2");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "prior.txt"), "prior-good-release");
            LocalReleaseService.DirBaseline baseline = LocalReleaseService.CaptureDirBaseline(dir); // existed -> backed up

            Directory.Delete(dir, true); // the run replaces it with half-released content
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "new.txt"), "half-released");

            LocalReleaseService.RestoreDirBaseline(dir, baseline);

            Assert.True(Directory.Exists(dir));
            Assert.Equal("prior-good-release", File.ReadAllText(Path.Combine(dir, "prior.txt")));
            Assert.False(File.Exists(Path.Combine(dir, "new.txt")), "the half-released content must be gone after restore");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void RestoreCycleState_restoresPriorBytes_andDeletesAFileThatWasAbsentBefore()
    {
        string repo = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(repo, ".doti"));
            string path = Path.Combine(repo, ".doti", "cycle-state.json");
            File.WriteAllText(path, "{\"before\":true}");
            byte[]? before = LocalReleaseService.CaptureCycleState(repo);

            File.WriteAllText(path, "{\"released\":true}"); // MarkReleaseTrainReleased mutates it
            LocalReleaseService.RestoreCycleState(repo, before);
            Assert.Equal("{\"before\":true}", File.ReadAllText(path));

            // bug-only repo: no cycle-state existed before -> restore must DELETE what the run wrote (BLOCKER-4)
            byte[]? none = LocalReleaseService.CaptureCycleState(Path.Combine(repo, "absent-subdir"));
            File.WriteAllText(path, "{\"released\":true}");
            LocalReleaseService.RestoreCycleState(repo, none);
            Assert.False(File.Exists(path), "a cycle-state the run created on a bug-only repo must be removed on rollback");
        }
        finally { Directory.Delete(repo, true); }
    }

    private static string NewTempDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "hx-relcomp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }
}
