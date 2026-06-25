using System.Text;
using Hx.Doti.Core.Workflow;

namespace Hx.Doti.Core;

/// <summary>
/// Renders one installed <c>SKILL.md</c> for a skill + agent flavor. Output is LF-only and
/// byte-stable so the drift check (<c>render-skills --check</c>) never false-positives on line
/// endings or platform differences (the Gitleaks CRLF lesson). Frontmatter is hand-emitted with
/// a fixed key order rather than via a YAML serializer, to reproduce the exact bytes.
/// </summary>
public static class SkillMarkdownRenderer
{
    private const char Lf = '\n';

    public static string Render(
        DotiSkillsManifest manifest, DotiSkillEntry skill, DotiAgentTarget agent, string availabilityFootnote)
    {
        DotiWorkflowStage stage = DotiWorkflowRegistry.FindByCommandName(skill.Name);
        string commandTemplate = $"{manifest.CommandTemplateDir}/{stage.CommandName}.md";
        var sb = new StringBuilder();

        AppendFrontmatter(sb, manifest, skill, stage, agent, commandTemplate);
        AppendBody(sb, manifest, skill, stage, commandTemplate, availabilityFootnote);

        return sb.ToString();
    }

    private static void AppendFrontmatter(
        StringBuilder sb,
        DotiSkillsManifest manifest,
        DotiSkillEntry skill,
        DotiWorkflowStage stage,
        DotiAgentTarget agent,
        string commandTemplate)
    {
        Line(sb, "---");
        Line(sb, $"name: {stage.SkillId}");
        Line(sb, $"description: {skill.Description}");
        Line(sb, "compatibility:");
        Line(sb, $"  - {agent.Compatibility}");
        Line(sb, "metadata:");
        Line(sb, $"  source: {commandTemplate}");
        Line(sb, $"  maturity: {manifest.Maturity}");
        if (agent.ClaudeFrontmatter)
        {
            Line(sb, $"argument-hint: \"{skill.ArgumentHint}\"");
            Line(sb, "user-invocable: true");
            Line(sb, "disable-model-invocation: false");
        }

        Line(sb, "---");
    }

    private static void AppendBody(
        StringBuilder sb,
        DotiSkillsManifest manifest,
        DotiSkillEntry skill,
        DotiWorkflowStage stage,
        string commandTemplate,
        string availabilityFootnote)
    {
        Line(sb, $"# {stage.SkillId}");
        Line(sb, "");
        Line(sb, manifest.IntroTemplate
            .Replace("{agentContextRef}", manifest.AgentContextRef)
            .Replace("{commandTemplate}", commandTemplate));
        Line(sb, "");
        foreach (string highlight in skill.Highlights)
        {
            Line(sb, highlight);
            Line(sb, "");
        }

        // Shared, manifest-level operator-question protocol — rendered identically into every
        // SKILL.md (and substituted into the agent context) so the format is in front of the agent
        // at every stage, for every agent flavor. Single source: skills.json operatorQuestionProtocol.
        if (!string.IsNullOrEmpty(manifest.OperatorQuestionProtocol))
        {
            Line(sb, manifest.OperatorQuestionProtocol);
            Line(sb, "");
        }

        Line(sb, $"Command availability: {availabilityFootnote}");
        Line(sb, "");
        Line(sb, $"Next stage: {stage.NextStep}");
    }

    private static void Line(StringBuilder sb, string text) => sb.Append(text).Append(Lf);
}
