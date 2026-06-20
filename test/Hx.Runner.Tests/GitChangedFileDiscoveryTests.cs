using Hx.Runner.Core.Git;
using Hx.Runner.Core.Process;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class GitChangedFileDiscoveryTests
{
    [Fact]
    public void DiscoversStagedAddedFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-git-discovery-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            RunGit(dir, "init");
            File.WriteAllText(Path.Combine(dir, "sample.txt"), "hello");
            RunGit(dir, "add", "sample.txt");

            IReadOnlyList<ChangedFile> files = GitChangedFileDiscovery.DiscoverStaged(dir);

            ChangedFile file = Assert.Single(files);
            Assert.Equal("sample.txt", file.Path);
            Assert.Equal(ChangeKind.Added, file.Kind);
        }
        finally
        {
            DeleteDirectory(dir);
        }
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessRunResult result = ProcessRunner.Run(new ToolCommand("git", arguments, workingDirectory));
        Assert.True(result.ExitCode == 0, result.StandardError);
    }

    private static void DeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }
}
