namespace Hx.Doti.Core;

/// <summary>
/// The single source for per-skill presentation (<c>doti/core/skills.json</c>). The renderer
/// composes each installed <c>SKILL.md</c> from these entries plus the uniform intro line, the
/// canonical availability footnote (from the profile), and per-agent frontmatter. Editing a
/// skill means editing this manifest and re-rendering — never the installed files by hand.
/// </summary>
public sealed record DotiSkillsManifest(
    int SchemaVersion,
    string Maturity,
    string CommandTemplateDir,
    string AgentContextRef,
    string IntroTemplate,
    string? OperatorQuestionProtocol,
    IReadOnlyList<DotiSkillEntry> Skills);

public sealed record DotiSkillEntry(
    string Name,
    string Description,
    string? ArgumentHint,
    IReadOnlyList<string> Highlights,
    string NextStage);
