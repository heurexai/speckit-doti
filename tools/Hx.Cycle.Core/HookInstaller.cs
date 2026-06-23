using System.Text;
using System.Security.Cryptography;
using Hx.Runner.Core.Process;

namespace Hx.Cycle.Core;

/// <summary>
/// Installs the insurance pre-commit hook into a repo's <b>untracked</b> git hooks directory (resolved via
/// <c>git rev-parse --git-path hooks</c>, so it respects worktrees / <c>core.hooksPath</c>). The hook is a
/// thin, logic-free POSIX-sh stub: it redirects a bare <c>git commit</c> to <c>doti cycle commit</c> by
/// checking the sentinel <c>cycle commit</c> sets — the verification logic stays in .NET. Because it lives
/// in <c>.git/</c> (not a tracked repo file) it honors the no-shell-runners policy, and because it is
/// git-local per-clone it must be (re)installed per clone via <c>doti install-hooks</c>.
/// </summary>
public static class HookInstaller
{
    public const string VerdictNotGitRepository = "not-git-repository";
    public const string VerdictMissing = "missing";
    public const string VerdictExpected = "expected";
    public const string VerdictDotiOwned = "doti-owned";
    public const string VerdictExternal = "external";

    private const string DotiHookMarker = "doti insurance pre-commit hook";
    private const string DotiCommitRedirect = "doti cycle commit";

    public static string HookScript =>
        "#!/bin/sh\n"
        + "# doti insurance pre-commit hook (installed by `doti install-hooks`; untracked, logic-free).\n"
        + "# Redirects a bare `git commit` to the sanctioned `doti cycle commit`; the verification is in .NET.\n"
        + $"if [ \"${PrecommitGuard.SentinelEnvVar}\" = \"1\" ]; then exit 0; fi\n"
        + "echo \"doti: this repo commits through the cycle - run: doti cycle commit --message <m>\" 1>&2\n"
        + "exit 1\n";

    public static DotiHookInspection Inspect(string repositoryRoot)
    {
        string expectedHash = Sha256(Encoding.UTF8.GetBytes(HookScript));
        ProcessRunResult hooks;
        try
        {
            hooks = ProcessRunner.Run(new ToolCommand("git", ["rev-parse", "--git-path", "hooks"], repositoryRoot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new DotiHookInspection(
                VerdictNotGitRepository, null, expectedHash, null, CanInstallOrRefresh: false,
                $"cannot resolve the git hooks directory for '{repositoryRoot}': {ex.Message}");
        }

        if (hooks.ExitCode != 0 || string.IsNullOrWhiteSpace(hooks.StandardOutput))
        {
            string detail = string.IsNullOrWhiteSpace(hooks.StandardError)
                ? "git did not return a hooks path"
                : hooks.StandardError.Trim();
            return new DotiHookInspection(
                VerdictNotGitRepository, null, expectedHash, null, CanInstallOrRefresh: false,
                $"cannot resolve the git hooks directory (is '{repositoryRoot}' a git repo?): {detail}");
        }

        string hooksDir = Path.GetFullPath(Path.Combine(repositoryRoot, hooks.StandardOutput.Trim()));
        string hookPath = Path.Combine(hooksDir, "pre-commit");
        if (!File.Exists(hookPath))
        {
            return new DotiHookInspection(
                VerdictMissing, hookPath, expectedHash, null, CanInstallOrRefresh: true,
                "Doti insurance pre-commit hook is missing and can be installed.");
        }

        byte[] currentBytes = File.ReadAllBytes(hookPath);
        string currentHash = Sha256(currentBytes);
        if (string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return new DotiHookInspection(
                VerdictExpected, hookPath, expectedHash, currentHash, CanInstallOrRefresh: true,
                "Doti insurance pre-commit hook is already current.");
        }

        string current = Encoding.UTF8.GetString(currentBytes);
        if (IsDotiOwned(current))
        {
            return new DotiHookInspection(
                VerdictDotiOwned, hookPath, expectedHash, currentHash, CanInstallOrRefresh: true,
                "Existing Doti-owned pre-commit hook is out of date and can be refreshed.");
        }

        return new DotiHookInspection(
            VerdictExternal, hookPath, expectedHash, currentHash, CanInstallOrRefresh: false,
            "Existing pre-commit hook is not owned by Doti; refusing to overwrite it automatically.");
    }

    public static DotiHookInstallResult InstallIfSafe(string repositoryRoot)
    {
        DotiHookInspection inspection = Inspect(repositoryRoot);
        if (inspection.Verdict == VerdictNotGitRepository)
        {
            return new DotiHookInstallResult(false, false, "skipped", inspection, inspection.Message);
        }

        if (!inspection.CanInstallOrRefresh || inspection.HookPath is null)
        {
            return new DotiHookInstallResult(false, false, "blocked", inspection, inspection.Message);
        }

        if (inspection.Verdict == VerdictExpected)
        {
            return new DotiHookInstallResult(true, false, "already-current", inspection, inspection.Message);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(inspection.HookPath)!);
        File.WriteAllText(inspection.HookPath, HookScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            ProcessRunner.Run(new ToolCommand("chmod", ["+x", inspection.HookPath], repositoryRoot));
        }

        string action = inspection.Verdict == VerdictDotiOwned ? "refreshed" : "installed";
        return new DotiHookInstallResult(
            true, true, action, Inspect(repositoryRoot),
            action == "refreshed"
                ? "Refreshed the Doti insurance pre-commit hook."
                : "Installed the Doti insurance pre-commit hook.");
    }

    public static string Install(string repositoryRoot)
    {
        DotiHookInstallResult result = InstallIfSafe(repositoryRoot);
        if (!result.Success || result.Inspection.HookPath is null)
        {
            throw new InvalidOperationException(result.Message);
        }

        return result.Inspection.HookPath;
    }

    private static bool IsDotiOwned(string current) =>
        current.Contains(DotiHookMarker, StringComparison.OrdinalIgnoreCase)
        || (current.Contains(PrecommitGuard.SentinelEnvVar, StringComparison.Ordinal)
            && current.Contains(DotiCommitRedirect, StringComparison.OrdinalIgnoreCase));

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed record DotiHookInspection(
    string Verdict,
    string? HookPath,
    string ExpectedSha256,
    string? CurrentSha256,
    bool CanInstallOrRefresh,
    string Message);

public sealed record DotiHookInstallResult(
    bool Success,
    bool Changed,
    string Action,
    DotiHookInspection Inspection,
    string Message);
