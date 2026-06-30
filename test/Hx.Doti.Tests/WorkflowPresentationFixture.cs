using Hx.Doti.Core.Workflow;

namespace Hx.Doti.Tests;

/// <summary>
/// 028 FR-010: a shared <see cref="DotiWorkflowPresentation"/> built over the canonical 9-stage <c>workflow.yml</c>
/// (the same stage chain the engine ships), so renderer tests that need model-backed skill identity + next-step prose
/// have a real projection without depending on the on-disk repo layout.
/// </summary>
internal static class WorkflowPresentationFixture
{
    private const string Workflow =
        "schemaVersion: 2\nname: t\nstages:\n" +
        "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n    next: [clarify]\n" +
        "  - id: clarify\n    command: 02-doti-clarify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: [specify]\n    next: [plan]\n" +
        "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [clarify]\n    next: [arch-review]\n" +
        "  - id: arch-review\n    command: 04-doti-arch-review\n    kind: review\n    produces: docs/reviews/{feature}-arch-review.md\n    prereqs: [plan]\n    next: [tasks]\n" +
        "  - id: tasks\n    command: 05-doti-tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [arch-review]\n    next: [analyze]\n" +
        "  - id: analyze\n    command: 06-doti-analyze\n    kind: review\n    produces: docs/reviews/{feature}-analyze-report.md\n    prereqs: [tasks]\n    next: [implement]\n" +
        "  - id: implement\n    command: 07-doti-implement\n    kind: diff\n    prereqs: [analyze]\n    next: [drift-review]\n" +
        "  - id: drift-review\n    command: 08-doti-drift-review\n    kind: review\n    produces: docs/reviews/{feature}-drift-review.md\n    prereqs: [implement]\n    next: [release, specify]\n" +
        "  - id: release\n    command: 09-doti-release\n    kind: release\n    prereqs: [drift-review]\n    next: []\n";

    public static DotiWorkflowPresentation Load()
    {
        string repo = Path.Combine(Path.GetTempPath(), "hx-wf-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "workflows", "doti"));
        File.WriteAllText(Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml"), Workflow);
        return DotiWorkflowPresentation.Load(repo);
    }
}
