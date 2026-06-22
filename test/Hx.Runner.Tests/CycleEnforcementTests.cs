using Hx.Cycle.Core;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// Enforcement: the operator-question validator (Layers B+C), the prereq-chain keystone, the
/// gate-proof store, and the fail-closed `cycle check`/`cycle commit` behavior (temp git repo fixtures).
/// </summary>
public sealed class CycleEnforcementTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-cycle15-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // git marks .git/objects entries read-only on Windows, so a plain recursive delete throws
    // UnauthorizedAccessException — clear attributes first, then delete (best-effort: the OS reclaims temp).
    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { /* temp dir; the OS reclaims it */ }
        catch (UnauthorizedAccessException) { /* temp dir; the OS reclaims it */ }
    }

    // ---------------- OperatorQuestionValidator (Layers B+C) ----------------

    private static OperatorQuestion ValidQuestion() => new(
        SchemaVersion: 1,
        Question: "Which way?",
        WhyItMatters: "It changes the build.",
        Options:
        [
            new OperatorQuestionOption("A", ["fast"], ["risky"], "we go fast"),
            new OperatorQuestionOption("B", ["safe"], ["slow"], "we go safe"),
        ],
        Recommendation: new OperatorRecommendation("A", "fast wins"),
        Assumptions: [new OperatorAssumption("x holds", true, null)],
        Confidence: new OperatorConfidence("High", "read the code"),
        Premises: [new OperatorPremise("x", "verified by reading the source")]);

    [Fact]
    public void OperatorQuestion_Conformant_Passes()
    {
        OperatorQuestionValidation v = OperatorQuestionValidator.Validate(ValidQuestion());
        Assert.True(v.Valid, string.Join("; ", v.Errors));
    }

    [Fact]
    public void OperatorQuestion_MalformedVariants_FailClosed()
    {
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Question = "  " }).Valid);
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { WhyItMatters = "" }).Valid);
        // an option missing its cons
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with
        {
            Options = [new OperatorQuestionOption("A", ["p"], [], "c"), new OperatorQuestionOption("B", ["p"], ["c"], "c")],
        }).Valid);
        // fewer than two options
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with
        {
            Options = [new OperatorQuestionOption("A", ["p"], ["c"], "c")],
        }).Valid);
        // recommendation names no real option
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Recommendation = new OperatorRecommendation("Z", "?") }).Valid);
        // confidence without a reason
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Confidence = new OperatorConfidence("High", " ") }).Valid);
        // an unverified assumption that does not say what would verify it
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Assumptions = [new OperatorAssumption("y", false, null)] }).Valid);
        // a premise without evidence
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Premises = [new OperatorPremise("p", "")] }).Valid);
    }

    // ---------------- Prereq-chain keystone (analyze F1) ----------------

    [Fact]
    public void StageModel_ParsesInlinePrereqs()
    {
        string dir = NewTempDir();
        try
        {
            string wf = Path.Combine(dir, "workflow.yml");
            File.WriteAllText(wf,
                "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    prereqs: []\n  - id: clarify\n    kind: doc\n    prereqs: [specify]\n  - id: commit\n    kind: commit\n    prereqs: [clarify]\n");
            StageModel model = StageModel.Load(wf);
            Assert.Empty(model.Find("specify").Prereqs);
            Assert.Equal("specify", Assert.Single(model.Find("clarify").Prereqs));
            Assert.Equal("clarify", Assert.Single(model.Find("commit").Prereqs));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    // ---------------- GateProofStore ----------------

    [Fact]
    public void GateProofStore_RoundTrips()
    {
        string dir = NewTempDir();
        try
        {
            var store = new GateProofStore(dir);
            Assert.Null(store.Read());

            var persisted = new PersistedGateProof(
                1, "id-1", "dev", Lane.Normal, new GateProof(1, StageOutcome.Pass, [], []), "abc123");
            store.Write(persisted);

            PersistedGateProof? read = store.Read();
            Assert.NotNull(read);
            Assert.Equal("id-1", read!.ChangeSetId);
            Assert.Equal(Lane.Normal, read.Lane);
            Assert.Equal(StageOutcome.Pass, read.Proof.Outcome);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void GateProofValidator_RejectsLegacyProofWithoutAffectedTestProof()
    {
        string dir = NewTempDir();
        try
        {
            var persisted = new PersistedGateProof(
                1, "id-1", "dev", Lane.Normal, new GateProof(1, StageOutcome.Pass, [], []), "abc123");

            IReadOnlyList<string> reasons = GateProofValidator.ValidateAffectedTestProof(dir, persisted);

            Assert.Contains(reasons, r => r.Contains("no affected-test proof", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    // ---------------- cycle check / commit (temp git repo) ----------------

    private static void Git(string dir, params string[] args)
    {
        ProcessRunResult r = ProcessRunner.Run(new ToolCommand("git", args, dir));
        if (r.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {r.StandardError.Trim()}");
        }
    }

    private static string InitRepo()
    {
        string dir = NewTempDir();
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@example.com");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "commit.gpgsign", "false");

        string wfDir = Path.Combine(dir, ".doti", "workflows", "doti");
        Directory.CreateDirectory(wfDir);
        File.WriteAllText(Path.Combine(wfDir, "workflow.yml"),
            "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n  - id: drift-review\n    kind: review\n    prereqs: [specify]\n  - id: commit\n    kind: commit\n    prereqs: [drift-review]\n");
        // Mirror the real scaffold: the cycle's own state files are gitignored, so stamping them does not
        // pollute the change set (otherwise writing cycle-state.json would itself flip stamps to stale).
        File.WriteAllText(Path.Combine(dir, ".gitignore"), ".doti/cycle-state.json\n.doti/gate-proof.json\n");
        File.WriteAllText(Path.Combine(dir, "seed.txt"), "seed");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "seed");
        return dir;
    }

    [Fact]
    public void Check_FailsClosed_WhenPrerequisitesAreMissing()
    {
        string dir = InitRepo();
        try
        {
            CycleCheckReport report = new CycleService(dir).Check("commit");
            Assert.False(report.Passed);
            Assert.Contains(report.Prerequisites, p => p.Stage == "specify" && p.Status == "missing");
            Assert.Contains(report.Prerequisites, p => p.Stage == "drift-review" && p.Status == "missing");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_FailsClosed_WhenPrerequisitesAreMissing()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("drift-review", "f", null));

            Assert.Contains("prerequisites are not all fresh", ex.Message);
            Assert.Contains("specify: missing", ex.Message);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Commit_Refuses_WhenPrerequisitesOrGateProofMissing()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "f.md"), "spec body");

            var service = new CycleService(dir);
            service.Stamp("specify", "f", null); // cycle-state now exists; only specify is stamped

            CycleCommitResult result = service.Commit("a message");
            Assert.False(result.Committed);
            Assert.Null(result.CommitSha);
            // refuses for at least the missing drift-review prerequisite and the absent gate proof
            Assert.Contains(result.Reasons, r => r.Contains("drift-review", StringComparison.Ordinal));
            Assert.Contains(result.Reasons, r => r.Contains("gate proof", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Check_FlagsRealClarificationMarker_ButNotaBareMention()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            string spec = Path.Combine(dir, "docs", "specs", "f.md");
            var service = new CycleService(dir);

            // A real open marker (colon + question) is flagged invalid.
            File.WriteAllText(spec, "spec body with a real [NEEDS CLARIFICATION: which database?] marker");
            service.Stamp("specify", "f", null);
            CycleCheckReport withMarker = service.Check("drift-review"); // drift-review's prereq is specify
            Assert.Contains(withMarker.Prerequisites, p => p.Stage == "specify" && p.Status == "invalid");

            // A bare, backticked mention of the convention is NOT a false positive.
            File.WriteAllText(spec, "spec body that mentions `[NEEDS CLARIFICATION]` only as documentation");
            service.Stamp("specify", "f", null); // re-stamp against the new content
            CycleCheckReport withMention = service.Check("drift-review");
            Assert.Contains(withMention.Prerequisites, p => p.Stage == "specify" && p.Status == "fresh");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void InsuranceHook_BlocksBareCommit_AllowsSanctioned()
    {
        string dir = InitRepo();
        try
        {
            HookInstaller.Install(dir);
            File.WriteAllText(Path.Combine(dir, "change.txt"), "x");
            Git(dir, "add", "-A");

            // A bare `git commit` (no sentinel) is blocked by the hook.
            ProcessRunResult bare = ProcessRunner.Run(new ToolCommand("git", ["commit", "-m", "bare"], dir));
            Assert.NotEqual(0, bare.ExitCode);

            // A sanctioned commit (sentinel set, as `cycle commit` does) is allowed through.
            ProcessRunResult sanctioned = ProcessRunner.Run(new ToolCommand(
                "git", ["commit", "-m", "sanctioned"], dir,
                new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" }));
            Assert.Equal(0, sanctioned.ExitCode);
        }
        finally
        {
            ForceDelete(dir);
        }
    }
}
