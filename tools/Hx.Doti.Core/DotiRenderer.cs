using System.Text;
using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// Renders the installed Doti skills (Claude + Codex) and the shared agent context from their
/// single sources (<c>doti/core/skills.json</c> + the canonical availability footnote in the
/// profile + the agent-context template), and drift-checks them. Self-contained IO (no
/// <c>Hx.Runner.Core</c> dependency — arch-review F1); output is byte-stable and LF-only.
/// </summary>
public static class DotiRenderer
{
    public const string ManifestRelativePath = "doti/core/skills.json";
    public const string ProfileRelativePath = "doti/profiles/dotnet-cli/profile.json";
    public const string AgentContextTemplateRelativePath = "doti/core/templates/agent-context-template.md";
    public const string AgentContextOutputRelativePath = ".doti/agent-context.md";

    public static DotiSkillsManifest LoadManifest(string repoRoot)
    {
        string path = Resolve(repoRoot, ManifestRelativePath);
        DotiSkillsManifest? manifest = JsonSerializer.Deserialize<DotiSkillsManifest>(
            File.ReadAllText(path), JsonContractSerializerOptions.Create());
        return manifest ?? throw new InvalidOperationException($"Doti skills manifest is empty: {ManifestRelativePath}");
    }

    public static string LoadAvailabilityFootnote(string repoRoot) =>
        LoadProfileString(repoRoot, "commandAvailabilityFootnote");

    public static string LoadRootMaturityNote(string repoRoot) =>
        LoadProfileString(repoRoot, "rootMaturityNote");

    private static string LoadProfileString(string repoRoot, string property)
    {
        string path = Resolve(repoRoot, ProfileRelativePath);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.TryGetProperty("selfHostingStatus", out JsonElement status)
            && status.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!;
        }

        throw new InvalidOperationException($"selfHostingStatus.{property} is missing from {ProfileRelativePath}.");
    }

    public static IReadOnlyList<DotiRenderTarget> BuildTargets(string repoRoot, IReadOnlyList<DotiAgentTarget> agents)
    {
        DotiSkillsManifest manifest = LoadManifest(repoRoot);
        string footnote = LoadAvailabilityFootnote(repoRoot);
        string rootMaturityNote = LoadRootMaturityNote(repoRoot);
        List<DotiRenderTarget> targets = [];

        foreach (DotiSkillEntry skill in manifest.Skills)
        {
            foreach (DotiAgentTarget agent in agents)
            {
                string content = SkillMarkdownRenderer.Render(manifest, skill, agent, footnote);
                targets.Add(new DotiRenderTarget($"{agent.SkillsRoot}/{skill.Name}/SKILL.md", content));
            }
        }

        // Thin root entrypoint per agent (CLAUDE.md / AGENTS.md) with a single-sourced shared block.
        foreach (DotiAgentTarget agent in agents)
        {
            targets.Add(new DotiRenderTarget(agent.RootEntrypointPath, RootEntrypointRenderer.Render(agent, rootMaturityNote)));
        }

        // Shared agent context is rendered from its template (single source), with the manifest-level
        // operator-question protocol substituted in so the same block that lands in every SKILL.md is
        // also in the agent context — one source (skills.json), no second literal copy.
        string contextTemplate = Resolve(repoRoot, AgentContextTemplateRelativePath);
        string contextContent = File.ReadAllText(contextTemplate);
        if (!string.IsNullOrEmpty(manifest.OperatorQuestionProtocol))
        {
            contextContent = contextContent.Replace("{operatorQuestionProtocol}", manifest.OperatorQuestionProtocol);
        }

        targets.Add(new DotiRenderTarget(AgentContextOutputRelativePath, contextContent));

        return targets;
    }

    public static DotiRenderResult Render(string repoRoot, IReadOnlyList<DotiAgentTarget> agents, bool check)
    {
        IReadOnlyList<DotiRenderTarget> targets = BuildTargets(repoRoot, agents);
        UTF8Encoding utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
        List<DotiRenderFileStatus> files = [];
        List<string> drifted = [];
        List<string> written = [];

        foreach (DotiRenderTarget target in targets)
        {
            string full = Resolve(repoRoot, target.RelativePath);
            byte[] desired = utf8NoBom.GetBytes(target.Content);
            bool existed = File.Exists(full);
            bool matches = existed && File.ReadAllBytes(full).AsSpan().SequenceEqual(desired);

            if (check)
            {
                files.Add(new DotiRenderFileStatus(target.RelativePath, matches, existed));
                if (!matches)
                {
                    drifted.Add(target.RelativePath);
                }

                continue;
            }

            if (!matches)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllBytes(full, desired);
                written.Add(target.RelativePath);
            }

            files.Add(new DotiRenderFileStatus(target.RelativePath, true, existed));
        }

        StageOutcome outcome = check && drifted.Count > 0 ? StageOutcome.Fail : StageOutcome.Pass;
        return new DotiRenderResult(JsonContractDefaults.SchemaVersion, outcome, check, files, drifted, written);
    }

    private static string Resolve(string repoRoot, string relativePath) =>
        Path.GetFullPath(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
