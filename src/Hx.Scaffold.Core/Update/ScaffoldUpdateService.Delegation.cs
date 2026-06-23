using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Scaffold.Core.Versioning;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    private static string? DelegationReason(ScaffoldVersionReport version, string runningVersion, string latestVersion)
    {
        if (Environment.GetEnvironmentVariable("HX_UPDATE_DELEGATED") == "1")
        {
            return null;
        }

        string running = ScaffoldVersionReporter.IdentityFromVersion(runningVersion, "running").NormalizedVersion;
        if (Hx.Version.Core.GitVersionTool.CompareVersions(running, latestVersion) < 0)
        {
            return $"running hx {running} is older than latest compatible release {latestVersion}";
        }

        if (version.Target is not null
            && Hx.Version.Core.GitVersionTool.CompareVersions(running, version.Target.NormalizedVersion) < 0)
        {
            return $"running hx {running} is older than target repo {version.Target.NormalizedVersion}";
        }

        return null;
    }

    private static ScaffoldUpdateDelegation RunDelegatedUpdater(
        UpdateCacheResult cache,
        string targetRepo,
        ScaffoldUpdateRequest request,
        string reason)
    {
        string payloadRoot = cache.PayloadRoot;
        string exe = Path.Combine(payloadRoot, OperatingSystem.IsWindows() ? "hx.exe" : "hx");
        if (!File.Exists(exe))
        {
            throw new InvalidOperationException("Delegated updater executable is missing from the verified release payload: " + exe);
        }

        string exeSha = VerifyDelegatedExecutable(cache.ArchivePath, cache.ExtractedPath, exe);
        var args = new List<string> { "update", "--repo", targetRepo, "--json" };
        if (request.DryRun) { args.Add("--dry-run"); }
        if (request.Force) { args.Add("--force"); }
        if (request.NoWorktree) { args.Add("--noworktree"); }
        ProcessRunResult run = Hx.Runner.Core.Process.ProcessRunner.Run(new ToolCommand(
            exe,
            args,
            targetRepo,
            new Dictionary<string, string> { ["HX_UPDATE_DELEGATED"] = "1" }));
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException("delegated updater failed: " +
                (string.IsNullOrWhiteSpace(run.StandardError) ? run.StandardOutput : run.StandardError));
        }

        return new ScaffoldUpdateDelegation(
            Required: true,
            reason,
            exe,
            exeSha,
            args,
            run.ExitCode,
            string.IsNullOrWhiteSpace(run.StandardOutput) ? run.StandardError : run.StandardOutput);
    }

    private static ScaffoldUpdateWorktreeBackup CreateBackupWorktree(string repositoryRoot, string worktreeRoot)
    {
        Directory.CreateDirectory(worktreeRoot);
        string repoName = Path.GetFileName(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string backup = Path.Combine(worktreeRoot, repoName + "-backup-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss"));
        string? head = TryHeadSha(repositoryRoot);
        ProcessRunResult run = Hx.Runner.Core.Process.ProcessRunner.Run(
            new ToolCommand("git", ["worktree", "add", "--detach", backup, "HEAD"], repositoryRoot));
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException("backup worktree creation failed: " + run.StandardError.Trim());
        }

        return new ScaffoldUpdateWorktreeBackup(
            backup,
            head,
            "HEAD",
            $"git -C \"{repositoryRoot}\" worktree remove --force \"{backup}\"",
            Created: true,
            Disabled: false,
            "Backup worktree created from committed HEAD before mutation. The original target checkout is the mutation target; staged, unstaged, and untracked edits are not captured by this backup.");
    }

    private static string VerifyDelegatedExecutable(string archivePath, string extractPath, string executablePath)
    {
        string executableFull = Path.GetFullPath(executablePath);
        string relative = Path.GetRelativePath(extractPath, executableFull).Replace('\\', '/');
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Delegated updater executable is outside the verified extraction root: " + executablePath);
        }

        string extractedSha = FileHashing.Sha256OfFile(executableFull);
        string archiveSha = HashEntryFromArchive(archivePath, relative)
            ?? throw new InvalidOperationException("Delegated updater executable is not present in the verified archive: " + relative);
        if (!string.Equals(extractedSha, archiveSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Delegated updater executable hash does not match the verified release archive.");
        }

        return extractedSha;
    }

    private static string? HashEntryFromArchive(string archivePath, string relativePath)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName.Replace('\\', '/'), relativePath, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return null;
            }

            using Stream stream = entry.Open();
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            using FileStream file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            for (TarEntry? entry = reader.GetNextEntry(); entry is not null; entry = reader.GetNextEntry())
            {
                if (!string.Equals(entry.Name.Replace('\\', '/'), relativePath, StringComparison.OrdinalIgnoreCase)
                    || entry.DataStream is null)
                {
                    continue;
                }

                return Convert.ToHexString(SHA256.HashData(entry.DataStream)).ToLowerInvariant();
            }
        }

        return null;
    }

    private static string TargetToLatestRelation(ScaffoldVersionIdentity? target, string? latestVersion)
    {
        if (target is null || string.IsNullOrWhiteSpace(latestVersion))
        {
            return ScaffoldVersionRelation.Unknown;
        }

        int compare = Hx.Version.Core.GitVersionTool.CompareVersions(target.NormalizedVersion, latestVersion);
        return compare switch
        {
            < 0 => ScaffoldVersionRelation.Behind,
            0 => ScaffoldVersionRelation.Equal,
            _ => ScaffoldVersionRelation.Newer,
        };
    }

    private static string? TryHeadSha(string repositoryRoot)
    {
        ProcessRunResult run = Hx.Runner.Core.Process.ProcessRunner.Run(
            new ToolCommand("git", ["rev-parse", "HEAD"], repositoryRoot));
        return run.ExitCode == 0 ? run.StandardOutput.Trim() : null;
    }
}
