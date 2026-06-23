using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult DotiRenderSkills(CliMeta meta, string repo, string agentsCsv, bool check)
    {
        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti render-skills", error!);
        }

        DotiRenderResult result = DotiRenderer.Render(repo, agents, check);
        string summary = result.Outcome == StageOutcome.Pass
            ? (check ? "No skill drift." : "Skills rendered.")
            : "Skill drift: " + string.Join(", ", result.Drifted);
        return CliResults.FromStage(meta, "doti render-skills", result.Outcome, summary, result);
    }
}
