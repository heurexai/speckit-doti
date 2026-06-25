using Hx.Doti.Core;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    private static bool TryParseAgents(string csv, out List<DotiAgentTarget> agents, out string? error)
    {
        if (!DotiAgentTarget.TryParseCsv(csv, out IReadOnlyList<DotiAgentTarget> parsed, out error))
        {
            agents = [];
            return false;
        }

        agents = parsed.ToList();
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
