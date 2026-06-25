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
        if (check && HasFullDotiPayloadShape(repo))
        {
            DotiPayloadCheckResult payloadCheck = DotiPayloadParityChecker.Check(repo);
            string[] drifted = result.Drifted
                .Concat(payloadCheck.Drifted.Select(path => "payload:" + path))
                .ToArray();
            StageOutcome outcome = result.Outcome == StageOutcome.Pass && payloadCheck.Outcome == StageOutcome.Pass
                ? StageOutcome.Pass
                : StageOutcome.Fail;
            result = result with
            {
                Outcome = outcome,
                Drifted = drifted,
                PayloadCheck = payloadCheck
            };
        }

        string summary = result.Outcome == StageOutcome.Pass
            ? (check
                ? result.PayloadCheck is null
                    ? "No skill drift."
                    : $"No skill or payload drift across {result.PayloadCheck.CheckedCount} managed payload file(s)."
                : "Skills rendered.")
            : "Skill drift: " + string.Join(", ", result.Drifted);
        return CliResults.FromStage(meta, "doti render-skills", result.Outcome, summary, result);
    }

    private static bool HasFullDotiPayloadShape(string repoRoot)
    {
        foreach (string relativePath in new[]
        {
            ".doti/core",
            ".doti/profiles",
            ".doti/templates",
            ".doti/memory",
            ".doti/workflows",
            ".doti/integrations"
        })
        {
            string full = Path.GetFullPath(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!Directory.Exists(full))
            {
                return false;
            }
        }

        return true;
    }
}
