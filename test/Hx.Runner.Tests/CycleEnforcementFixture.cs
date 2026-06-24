using Hx.Cycle.Core;
using Hx.Impact.Core.Planning;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-cycle15-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

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
        File.WriteAllText(Path.Combine(dir, "test.slnx"), "<Solution></Solution>\n");
        File.WriteAllText(Path.Combine(dir, ".gitignore"), ".doti/cycle-state.json\n.doti/gate-proof.json\n");
        File.WriteAllText(Path.Combine(dir, "seed.txt"), "seed");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "seed");
        return dir;
    }

    private static void WritePassingGateProofForCurrentDiff(string dir)
    {
        CycleState state = new CycleStateStore(dir).Read()
            ?? throw new InvalidOperationException("cycle state missing");
        string changeSetId = ChangeSetIdentity.Of(dir, state.BaseRef, "HEAD");
        AffectedPlan plan = new AffectedTestPlanner().Plan(dir, state.BaseRef, "HEAD", "Release");
        var affectedProof = new AffectedTestProof(
            JsonContractDefaults.SchemaVersion,
            state.BaseRef,
            "HEAD",
            "Release",
            AffectedTestProofHasher.HashPlan(plan),
            AffectedTestProofHasher.HashTestScope([]),
            AffectedTestProofHasher.HashExecutedTests([]),
            FullSuite: false,
            FullSuiteReason: null,
            plan,
            []);
        var gateProof = new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Pass, [], [], affectedProof);
        new GateProofStore(dir).Write(new PersistedGateProof(
            JsonContractDefaults.SchemaVersion,
            changeSetId,
            state.BaseRef,
            Lane.Normal,
            gateProof,
            GitHead(dir)));
    }

    private static string GitHead(string dir)
    {
        ProcessRunResult r = ProcessRunner.Run(new ToolCommand("git", ["rev-parse", "HEAD"], dir));
        if (r.ExitCode != 0)
        {
            throw new InvalidOperationException(r.StandardError.Trim());
        }

        return r.StandardOutput.Trim();
    }

    private static void PrepareDocsOnlyCycle(string dir, CycleService service)
    {
        Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
        Git(dir, "add", "docs/specs/001-f.md");
        service.Stamp("specify", "001-f", null);
        service.Stamp("drift-review", null, null);
        WritePassingGateProofForCurrentDiff(dir);
    }

    private static CycleCompletionIntent PendingIntentFor(CycleState state, string dir, string messageHash = "message-hash") =>
        new(
            JsonContractDefaults.SchemaVersion,
            state.Feature,
            state.CurrentStage,
            state.BaseRef,
            GitHead(dir),
            ChangeSetIdentity.Of(dir, state.BaseRef, "HEAD"),
            ChangeSetIdentity.Of(dir, state.BaseRef, "HEAD"),
            messageHash,
            DateTimeOffset.UtcNow.ToString("O"));
}
