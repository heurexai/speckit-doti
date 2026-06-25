using Hx.Doti.Core;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    private static bool TryParseAgents(string csv, out List<DotiAgentTarget> agents, out string? error)
    {
        agents = [];
        foreach (string key in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            DotiAgentTarget? agent = DotiAgentTarget.FromKey(key);
            if (agent is null)
            {
                error = $"Unknown agent '{key}'. Known: codex, claude.";
                return false;
            }

            agents.Add(agent);
        }

        if (agents.Count == 0)
        {
            agents.AddRange(DotiAgentTarget.All);
        }

        error = null;
        return true;
    }

    private static string? FindDotiSource(string start)
    {
        DirectoryInfo? dir = new(Path.GetFullPath(start));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".doti", "core", "skills.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
