namespace Hx.Doti.Core;

/// <summary>
/// A render target: an agent flavor, its installed skills directory, and whether it uses the
/// Claude-only frontmatter keys (<c>argument-hint</c>, <c>user-invocable</c>,
/// <c>disable-model-invocation</c>). Proven this session: Codex registers skills correctly
/// without those keys; Claude uses them.
/// </summary>
public sealed record DotiAgentTarget(
    string Key,
    string Compatibility,
    string SkillsRoot,
    bool ClaudeFrontmatter,
    string Title,
    string RootEntrypointPath,
    string SkillsGlob)
{
    public static readonly DotiAgentTarget Claude =
        new("claude", "claude", ".claude/skills", true, "Claude", "CLAUDE.md", ".claude/skills/doti-*");

    public static readonly DotiAgentTarget Codex =
        new("codex", "codex", ".agents/skills", false, "Codex", "AGENTS.md", ".agents/skills/doti-*");

    public static IReadOnlyList<DotiAgentTarget> All { get; } = [Claude, Codex];

    public static DotiAgentTarget? FromKey(string key) => key.Trim().ToLowerInvariant() switch
    {
        "claude" => Claude,
        "codex" => Codex,
        _ => null,
    };
}
