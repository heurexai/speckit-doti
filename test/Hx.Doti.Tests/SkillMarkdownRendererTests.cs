using Hx.Doti.Core;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class SkillMarkdownRendererTests
{
    private static DotiSkillsManifest Manifest(params DotiSkillEntry[] skills) => new(
        SchemaVersion: 1,
        Maturity: "command-aware-advisory",
        CommandTemplateDir: ".doti/core/templates/commands",
        AgentContextRef: ".doti/agent-context.md",
        IntroTemplate: "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
        OperatorQuestionProtocol: null,
        Skills: skills);

    private static readonly DotiSkillEntry Sample = new(
        Name: "doti-specify",
        Description: "Create or refine a scaffold-dotnet specification in command-aware-advisory mode.",
        ArgumentHint: "[feature or goal]",
        Highlights: [],
        NextStage: "Run `/doti-clarify` to resolve ambiguities, then `/doti-plan`.");

    private const string Footnote = "Hygiene commands exist; remaining gates advisory.";

    [Fact]
    public void OutputIsLfOnlyAndByteStable()
    {
        DotiSkillsManifest m = Manifest(Sample);
        string a = SkillMarkdownRenderer.Render(m, Sample, DotiAgentTarget.Claude, Footnote);
        string b = SkillMarkdownRenderer.Render(m, Sample, DotiAgentTarget.Claude, Footnote);

        Assert.DoesNotContain("\r", a);          // LF-only, never CRLF
        Assert.Equal(a, b);                       // idempotent / deterministic
        Assert.Contains("name: 01-doti-specify", a);
        Assert.Contains("# 01-doti-specify", a);
        Assert.Contains($"Command availability: {Footnote}", a);
        Assert.Contains("Next stage: Run `/02-doti-clarify`", a);
        Assert.Contains("follow `.doti/core/templates/commands/doti-specify.md`", a);
    }

    [Fact]
    public void ClaudeFlavorHasTheClaudeOnlyKeys_CodexDoesNot()
    {
        DotiSkillsManifest m = Manifest(Sample);
        string claude = SkillMarkdownRenderer.Render(m, Sample, DotiAgentTarget.Claude, Footnote);
        string codex = SkillMarkdownRenderer.Render(m, Sample, DotiAgentTarget.Codex, Footnote);

        Assert.Contains("  - claude", claude);
        Assert.Contains("argument-hint: \"[feature or goal]\"", claude);
        Assert.Contains("user-invocable: true", claude);
        Assert.Contains("disable-model-invocation: false", claude);

        Assert.Contains("  - codex", codex);
        Assert.DoesNotContain("argument-hint:", codex);
        Assert.DoesNotContain("user-invocable:", codex);
        Assert.DoesNotContain("disable-model-invocation:", codex);
    }

    [Fact]
    public void HighlightsAreEmittedInOrderBetweenIntroAndFooter()
    {
        DotiSkillEntry withHighlights = Sample with
        {
            Name = "doti-clarify",
            Highlights = ["First highlight paragraph.", "Second highlight paragraph."],
        };
        string rendered = SkillMarkdownRenderer.Render(Manifest(withHighlights), withHighlights, DotiAgentTarget.Claude, Footnote);

        int intro = rendered.IndexOf("follow `.doti/core", System.StringComparison.Ordinal);
        int first = rendered.IndexOf("First highlight", System.StringComparison.Ordinal);
        int second = rendered.IndexOf("Second highlight", System.StringComparison.Ordinal);
        int footer = rendered.IndexOf("Command availability:", System.StringComparison.Ordinal);

        Assert.True(intro < first && first < second && second < footer);
    }

    [Fact]
    public void OperatorQuestionProtocolIsRenderedIdenticallyForEverySkill()
    {
        // Layer A: the manifest-level operator-question protocol is the single source, rendered
        // byte-identically into every SKILL.md (and substituted into the agent context elsewhere).
        const string Protocol =
            "## Asking the operator a question (required format)\n\n- **Context** — restate the facts.\n- **Confidence** — High / Medium / Low.";
        DotiSkillsManifest m = new(
            SchemaVersion: 1,
            Maturity: "command-aware-advisory",
            CommandTemplateDir: ".doti/core/templates/commands",
            AgentContextRef: ".doti/agent-context.md",
            IntroTemplate: "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
            OperatorQuestionProtocol: Protocol,
            Skills: []);
        DotiSkillEntry specify = Sample;
        DotiSkillEntry clarify = Sample with { Name = "doti-clarify", NextStage = "Run `/doti-plan`." };

        string a = SkillMarkdownRenderer.Render(m, specify, DotiAgentTarget.Claude, Footnote);
        string b = SkillMarkdownRenderer.Render(m, clarify, DotiAgentTarget.Codex, Footnote);

        Assert.Contains(Protocol, a);                                   // present in every skill...
        Assert.Contains(Protocol, b);                                   // ...byte-identical (one source string)
        Assert.True(a.IndexOf(Protocol, System.StringComparison.Ordinal)
            < a.IndexOf("Command availability:", System.StringComparison.Ordinal)); // before the footer
    }

    [Fact]
    public void OperatorQuestionProtocolIsOmittedWhenManifestHasNone()
    {
        string rendered = SkillMarkdownRenderer.Render(Manifest(Sample), Sample, DotiAgentTarget.Claude, Footnote);
        Assert.DoesNotContain("Asking the operator a question", rendered); // null protocol ⇒ no block
    }
}
