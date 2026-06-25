using Hx.Cycle.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
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

    private static void PrepareDocsOnlyCycle(string dir, CycleService service)
    {
        Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
        WriteCompletedTaskFile(dir, "001-f");
        Git(dir, "add", "docs/tasks/001-f-tasks.md");
        Git(dir, "commit", "-q", "-m", "seed 001 task file");
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
        Git(dir, "add", "docs/specs/001-f.md");
        service.Stamp("specify", "001-f", null);
        service.Stamp("drift-review", null, null);
        WritePassingGateProofForCurrentDiff(dir);
    }

    private static void CompleteSecondCycleToRelease(string dir, CycleService service)
    {
        service.Stamp("specify", "002-next", null);
        WriteCompletedTaskFile(dir, "002-next");
        Git(dir, "add", "docs/tasks/002-next-tasks.md");
        Git(dir, "commit", "-q", "-m", "seed 002 task file");
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "002-next.md"), "second spec body");
        Git(dir, "add", "docs/specs/002-next.md");
        service.Stamp("specify", "002-next", null);
        service.Stamp("drift-review", null, null);
        WritePassingGateProofForCurrentDiff(dir);
        service.Stamp("release", null, null);
    }

}
