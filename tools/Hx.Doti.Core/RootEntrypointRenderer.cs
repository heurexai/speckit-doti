using System.Text;

namespace Hx.Doti.Core;

/// <summary>
/// Renders a thin root entrypoint (<c>CLAUDE.md</c> / <c>AGENTS.md</c>) for an agent. The
/// maturity/availability block is single-sourced (profile <c>rootMaturityNote</c>) and emitted
/// identically into every entrypoint, so the intentionally-duplicated content cannot diverge —
/// this is the "hash-checked shared block" guarantee (identical by construction). LF-only.
/// </summary>
public static class RootEntrypointRenderer
{
    private const char Lf = '\n';

    public static string Render(DotiAgentTarget agent, string maturityNote)
    {
        var sb = new StringBuilder();
        Line(sb, $"# {agent.Title} Entry Point");
        Line(sb, "");
        Line(sb, $"This file is a command-aware-advisory entrypoint for {agent.Title}.");
        Line(sb, "");
        Line(sb, $"Read `.doti/agent-context.md` first. Use local {agent.Title} skills under `{agent.SkillsGlob}`.");
        Line(sb, "");
        Line(sb, maturityNote);
        return sb.ToString();
    }

    private static void Line(StringBuilder sb, string text) => sb.Append(text).Append(Lf);
}
