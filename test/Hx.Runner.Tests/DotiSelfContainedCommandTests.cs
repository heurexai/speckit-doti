using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Runner.Cli;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 031 T014 (FR-001/002/011, SC-001): the CLI seam for the self-contained update commands. The source defaults to the
/// running tool's bundled payload and falls back to the working-directory dev walk — in-test the test bin has no
/// <c>.doti</c> beside it, so it resolves the repo root via the dev walk (proving the resolution does not regress).
/// The resolved source origin is threaded into the envelope, and <c>--no-commit</c> reaches the command. A non-Doti
/// <c>--repo</c> is reported (Validation), never mutated.
/// </summary>
public sealed class DotiSelfContainedCommandTests
{
    private static readonly CliMeta Meta = new("hx-runner", "0.0.0-test");

    [Fact]
    public void Update_requires_an_explicit_repo()
    {
        CliResult r = RunnerCommands.DotiUpdate(Meta, repo: null, "codex", force: false, dryRun: false, noCommit: false);
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Usage, r.ExitCode);
    }

    [Fact]
    public void Update_all_requires_an_explicit_root()
    {
        CliResult r = RunnerCommands.DotiUpdateAll(Meta, root: null, "codex", force: false, dryRun: false, noCommit: false);
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Usage, r.ExitCode);
    }

    [Fact]
    public void Update_on_a_non_doti_repo_is_reported_with_source_origin_and_not_mutated()
    {
        string target = Path.Combine(Path.GetTempPath(), "hx-runner-doti-update-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(target);
        try
        {
            // The source resolves via the dev-cwd fallback (the test runs under the repo). A non-Doti --repo is
            // reported NotARepo — proving the resolution threaded through and the command reached the reconciler.
            CliResult r = RunnerCommands.DotiUpdate(Meta, target, "codex", force: false, dryRun: false, noCommit: false);

            Assert.False(r.Ok);
            Assert.Equal((int)ExitClass.Validation, r.ExitCode);
            Assert.Equal(ErrorCodes.Validation_DotiNotARepo, Assert.Single(r.Errors).Code);
            // FR-011: the resolved source origin is in the envelope (dev-cwd in-test).
            using JsonDocument doc = JsonDocument.Parse(r.Data!.ToJsonString());
            Assert.Equal(DotiSourceOrigin.DevCwd, doc.RootElement.GetProperty("sourceOrigin").GetString());
            Assert.False(Directory.Exists(Path.Combine(target, ".doti")), "a non-Doti dir must not be mutated");
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }
}
