using System.Text;
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
    public static string HookScript =>
        "#!/bin/sh\n"
        + "# doti insurance pre-commit hook (installed by `doti install-hooks`; untracked, logic-free).\n"
        + "# Redirects a bare `git commit` to the sanctioned `doti cycle commit`; the verification is in .NET.\n"
        + $"if [ \"${PrecommitGuard.SentinelEnvVar}\" = \"1\" ]; then exit 0; fi\n"
        + "echo \"doti: this repo commits through the cycle - run: doti cycle commit --message <m>\" 1>&2\n"
        + "exit 1\n";

    public static string Install(string repositoryRoot)
    {
        ProcessRunResult hooks = ProcessRunner.Run(
            new ToolCommand("git", ["rev-parse", "--git-path", "hooks"], repositoryRoot));
        if (hooks.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"cannot resolve the git hooks directory (is '{repositoryRoot}' a git repo?): {hooks.StandardError.Trim()}");
        }

        string hooksDir = Path.GetFullPath(Path.Combine(repositoryRoot, hooks.StandardOutput.Trim()));
        Directory.CreateDirectory(hooksDir);
        string hookPath = Path.Combine(hooksDir, "pre-commit");
        File.WriteAllText(hookPath, HookScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (!OperatingSystem.IsWindows())
        {
            ProcessRunner.Run(new ToolCommand("chmod", ["+x", hookPath], repositoryRoot));
        }

        return hookPath;
    }
}
