using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Runner.Core.Process;
using System.Text;

namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    private static IReadOnlyList<DesiredManagedFile> BuildDesiredFiles(string payloadRoot)
    {
        var files = new Dictionary<string, DesiredManagedFile>(StringComparer.OrdinalIgnoreCase);
        void AddFile(string relativePath, string category, byte[] content) =>
            files[relativePath.Replace('\\', '/')] = new DesiredManagedFile(relativePath.Replace('\\', '/'), category, content);

        foreach ((string Root, string Category) root in ManagedCopyRoots(payloadRoot))
        {
            if (!Directory.Exists(root.Root))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(root.Root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(payloadRoot, file).Replace('\\', '/');
                if (IsLiveOrGeneratedMetadata(relative))
                {
                    continue;
                }

                string category = relative.StartsWith(".doti/workflows/", StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith("doti/core/templates/", StringComparison.OrdinalIgnoreCase)
                    ? ManagedAssetCategory.WorkflowTemplate
                    : root.Category;
                AddFile(relative, category, File.ReadAllBytes(file));
            }
        }

        foreach (DotiRenderTarget target in DotiRenderer.BuildTargets(payloadRoot, DotiAgentTarget.All))
        {
            AddFile(target.RelativePath, ManagedAssetCategory.SkillGeneratedInstruction,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(target.Content));
        }

        string prerequisites = Path.Combine(payloadRoot, "doti", "core", "prerequisites.json");
        if (File.Exists(prerequisites))
        {
            AddFile(".doti/prerequisites.json", ManagedAssetCategory.Metadata, File.ReadAllBytes(prerequisites));
        }

        return files.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<(string Root, string Category)> ManagedCopyRoots(string payloadRoot)
    {
        yield return (Path.Combine(payloadRoot, "doti", "core"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, "doti", "profiles"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, ".doti", "templates"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, ".doti", "memory"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, ".doti", "workflows"), ManagedAssetCategory.WorkflowTemplate);
        yield return (Path.Combine(payloadRoot, ".doti", "integrations"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, "tools", "gitleaks", "config"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, "tools", "gitleaks"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, "tools", "sentrux"), ManagedAssetCategory.DotiSource);
        yield return (Path.Combine(payloadRoot, "tools", "gitversion"), ManagedAssetCategory.DotiSource);
    }

    private static bool IsLiveOrGeneratedMetadata(string relativePath) =>
        relativePath is ".doti/agent-context.md" or ".doti/managed-assets.json" or ".doti/scaffold-version.json"
            or ".doti/cycle-state.json" or ".doti/gate-proof.json" or ".doti/integration.json" or ".doti/init-options.json"
        || relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
        || relativePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> DirtyPlannedPathBlockers(string repositoryRoot, IEnumerable<string> plannedPaths)
    {
        var planned = plannedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ProcessRunResult status = Hx.Runner.Core.Process.ProcessRunner.Run(new ToolCommand(
            "git", ["status", "--porcelain=v1", "-z", "--ignored", "--untracked-files=all"], repositoryRoot));
        if (status.ExitCode != 0)
        {
            yield return "could not inspect dirty managed paths: " + status.StandardError.Trim();
            yield break;
        }

        foreach (string path in ParsePorcelainPaths(status.StandardOutput))
        {
            if (planned.Contains(path))
            {
                yield return "dirty managed path: " + path;
            }
        }
    }

    private static ManagedFilePlan BuildFilePlan(string targetRoot, IReadOnlyList<DesiredManagedFile> desired)
    {
        var create = new List<string>();
        var replace = new List<string>();
        foreach (DesiredManagedFile file in desired)
        {
            string full = Path.GetFullPath(Path.Combine(targetRoot, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(full))
            {
                create.Add(file.Path);
            }
            else if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(file.Content))
            {
                replace.Add(file.Path);
            }
        }

        return new ManagedFilePlan(
            create.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
            replace.OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<string> ParsePorcelainPaths(string output)
    {
        string[] tokens = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token.Length < 4)
            {
                continue;
            }

            string statusCode = token[..2];
            yield return token[3..].Replace('\\', '/');
            if ((statusCode.Contains('R') || statusCode.Contains('C')) && i + 1 < tokens.Length)
            {
                i++;
            }
        }
    }

    private static IEnumerable<string> ApplyManagedFiles(string targetRoot, IReadOnlyList<DesiredManagedFile> desired)
    {
        foreach (DesiredManagedFile file in desired)
        {
            string full = Path.GetFullPath(Path.Combine(targetRoot, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsInside(targetRoot, full))
            {
                throw new InvalidOperationException("planned write escapes target root: " + file.Path);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            bool changed = !File.Exists(full) || !File.ReadAllBytes(full).AsSpan().SequenceEqual(file.Content);
            if (changed)
            {
                File.WriteAllBytes(full, file.Content);
                yield return file.Path;
            }
        }
    }

    private static IReadOnlyList<string> PossibleLegacyOrphans(string targetRoot, ISet<string> desiredPaths)
    {
        string[] roots = [".agents/skills", ".claude/skills", ".doti/workflows", "doti/core/templates"];
        List<string> orphans = [];
        foreach (string root in roots)
        {
            string fullRoot = Path.Combine(targetRoot, root.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(targetRoot, file).Replace('\\', '/');
                if (!desiredPaths.Contains(relative))
                {
                    orphans.Add(relative);
                }
            }
        }

        return orphans.OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> PreservedLivePaths(string targetRoot)
    {
        string[] candidates = [".sentrux", ".doti/cycle-state.json", ".doti/gate-proof.json"];
        return candidates.Where(p => File.Exists(Path.Combine(targetRoot, p.Replace('/', Path.DirectorySeparatorChar)))
                || Directory.Exists(Path.Combine(targetRoot, p.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
    }

    private static bool IsInside(string root, string fullPath)
    {
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
