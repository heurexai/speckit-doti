using System.Text;
using Hx.Cycle.Core;
using Hx.Cycle.Core.Actions;

namespace Hx.Doti.Core.Workflow;

/// <summary>
/// 028 FR-010 / SC-010: renders the agent-context <c>{commandAvailability}</c> block — the WORKFLOW-affordance command
/// availability — entirely from the engine's <see cref="DotiActionModel"/> + <see cref="StageModel"/> + the rehomed
/// <see cref="DotiWorkflowPresentation"/>. No hand-authored workflow command info: the numbered <c>/01</c>–<c>/09</c>
/// stage chain (each stage's advance command + next-step + its model-derived "available when …"), the reconcile
/// affordances (the reviewed-no-impact verb, the recovery menu tiers, the publish-boundary STOP), and the 7 utility
/// skills are all projected from the model. Output is LF-only + deterministic-order so the gate's byte-exact render
/// check never false-positives. Payload-derived CLI command info (the deterministic <c>hx</c>/<c>dotnet</c> catalog) is
/// out of scope (FR-010/B5) and stays as static template prose under <c>## Current Command Availability</c>.
/// </summary>
public static class CommandAvailabilityRenderer
{
    private const char Lf = '\n';

    public static string Render(DotiWorkflowPresentation workflow, DotiActionModel model)
    {
        var sb = new StringBuilder();
        Line(sb, "The valid workflow next-actions are generated from the cycle action model (no hand-authored command info). Each affordance below carries its exact invocation and the condition it is available under.");
        Line(sb, "");
        AppendStageChain(sb, workflow);
        Line(sb, "");
        AppendReconcileAffordances(sb, model);
        Line(sb, "");
        AppendUtilitySkills(sb, model);
        return sb.ToString().TrimEnd(Lf);
    }

    /// <summary>The numbered <c>/01</c>–<c>/09</c> stage chain: each stage's skill id, its advance command + the
    /// "available when …" the advance descriptor declares, and the branching next-step prose.</summary>
    private static void AppendStageChain(StringBuilder sb, DotiWorkflowPresentation workflow)
    {
        Line(sb, "Numbered cycle stages (`/01-doti-specify` through `/09-doti-release`), in order:");
        Line(sb, "");
        foreach (DotiWorkflowStage stage in workflow.Stages)
        {
            Line(sb, $"- `/{stage.SkillId}` ({stage.StageStatus}) — {stage.NextStep}");
        }
    }

    /// <summary>The reconcile affordances from the model: the reviewed-no-impact verb, the recovery-menu tiers, and the
    /// publish-boundary STOP — each with its model-derived "available when …".</summary>
    private static void AppendReconcileAffordances(StringBuilder sb, DotiActionModel model)
    {
        Line(sb, "Reconcile + boundary affordances (surfaced on the cycle/recovery result the agent already reads):");
        Line(sb, "");
        foreach (CommandDescriptor descriptor in ReconcileDescriptors(model))
        {
            Line(sb, AffordanceLine(descriptor));
        }
    }

    /// <summary>The 7 utility skills — modeled as always-available <see cref="CommandKind.Utility"/> descriptors so
    /// deleting <c>skills.json nextStage</c> never strands them (B6).</summary>
    private static void AppendUtilitySkills(StringBuilder sb, DotiActionModel model)
    {
        Line(sb, "Unnumbered utility skills (run by name, anytime — always available, never reordering `/01`–`/09`):");
        Line(sb, "");
        foreach (CommandDescriptor descriptor in model.Descriptors.Where(d => d.Kind == CommandKind.Utility))
        {
            Line(sb, $"- `{descriptor.InvocationTemplate}` — {descriptor.Why}");
        }
    }

    /// <summary>The static reconcile/boundary descriptors, in a fixed presentation order (the start-feature affordance,
    /// the reviewed-rebind verb, the publish boundary, the train loop) — deterministic for the byte-exact render.</summary>
    private static IEnumerable<CommandDescriptor> ReconcileDescriptors(DotiActionModel model)
    {
        string[] order =
        [
            DotiActionModel.StartFeatureId,
            DotiActionModel.ReviewRebindVerbId,
            DotiActionModel.PublishBoundaryId,
            DotiActionModel.TrainStartNextId,
        ];
        foreach (string id in order)
        {
            CommandDescriptor? descriptor = model.Descriptors.FirstOrDefault(d => d.Id == id);
            if (descriptor is not null)
            {
                yield return descriptor;
            }
        }
    }

    private static string AffordanceLine(CommandDescriptor descriptor)
    {
        string invocation = descriptor.InvocationTemplate is { } template
            ? $"`{template}`"
            : "STOP (no command — operator decision)";
        return $"- {invocation} — {descriptor.Why} Available when {descriptor.AvailableWhen()}.";
    }

    private static void Line(StringBuilder sb, string text) => sb.Append(text).Append(Lf);
}
