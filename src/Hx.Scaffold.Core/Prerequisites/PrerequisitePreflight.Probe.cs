using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Scaffold.Core.Prerequisites;

public static partial class PrerequisitePreflight
{
    private static string? LevelFor(PrerequisiteRequirement requirement, string command)
    {
        if (requirement.HardFor.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            return "hard";
        }

        return requirement.AdvisoryFor.Contains(command, StringComparer.OrdinalIgnoreCase) ? "advisory" : null;
    }

    private static PrerequisiteCheckItem Probe(
        PrerequisiteRequirement requirement,
        string level,
        PrerequisiteCheckRequest request,
        PrerequisiteServices services)
    {
        try
        {
            ProcessRunResult result = services.RunProcess(
                requirement.Probe.Executable,
                requirement.Probe.Arguments,
                string.IsNullOrWhiteSpace(request.RepositoryRoot) ? request.SourceRoot : request.RepositoryRoot!);
            string output = (result.StandardOutput + "\n" + result.StandardError).Trim();
            if (result.ExitCode != 0)
            {
                return Item(requirement, level, "missing", null, "probe failed: " + FirstOutput(result));
            }

            string? detected = ExtractVersion(output, requirement.Probe.VersionPattern);
            if (detected is null)
            {
                return Item(requirement, level, "unsupported", null, "could not parse version from probe output");
            }

            if (!string.IsNullOrWhiteSpace(requirement.MinimumVersion)
                && GitVersionTool.CompareVersions(detected, requirement.MinimumVersion) < 0)
            {
                return Item(requirement, level, "unsupported", detected,
                    $"requires >= {requirement.MinimumVersion}");
            }

            return Item(requirement, level, "found", detected, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return Item(requirement, level, "missing", null, ex.Message);
        }
    }

    private static PrerequisiteCheckItem Item(
        PrerequisiteRequirement requirement,
        string level,
        string status,
        string? detectedVersion,
        string? reason) =>
        new(
            requirement.Id,
            requirement.DisplayName,
            level,
            status,
            detectedVersion,
            requirement.MinimumVersion,
            requirement.Probe.Executable,
            reason,
            requirement.Winget);

    private static string? ExtractVersion(string output, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        MatchCollection matches = regex.Matches(output);
        string? best = null;
        foreach (Match match in matches)
        {
            string value = match.Groups["version"].Success ? match.Groups["version"].Value : match.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            best = best is null || GitVersionTool.CompareVersions(value, best) > 0 ? value : best;
        }

        return best;
    }

    private static IReadOnlyList<PrerequisiteDirectoryCheck> CheckDirectories(PrerequisiteCheckRequest request)
    {
        var checks = new List<PrerequisiteDirectoryCheck>
        {
            DirectoryCheck("source-root", request.SourceRoot, Directory.Exists(request.SourceRoot),
                Directory.Exists(request.SourceRoot) ? null : "source root does not exist"),
            DirectoryCheck("temp", Path.GetTempPath(), Directory.Exists(Path.GetTempPath()),
                Directory.Exists(Path.GetTempPath()) ? null : "temp directory does not exist"),
        };

        if (request.Command == PrerequisiteCommands.New && !string.IsNullOrWhiteSpace(request.OutputPath))
        {
            string output = Path.GetFullPath(request.OutputPath);
            string? parent = Path.GetDirectoryName(output);
            bool parentOk = !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent);
            checks.Add(DirectoryCheck("output-parent", parent ?? output, parentOk,
                parentOk ? null : "output parent directory does not exist"));
            checks.Add(DirectoryCheck("output-path", output, !File.Exists(output),
                File.Exists(output) ? "output path is an existing file" : null));
        }

        if (request.Command == PrerequisiteCommands.Version
            && !string.IsNullOrWhiteSpace(request.RepositoryRoot))
        {
            string repo = Path.GetFullPath(request.RepositoryRoot);
            checks.Add(DirectoryCheck("repo", repo, Directory.Exists(repo),
                Directory.Exists(repo) ? null : "repository directory does not exist"));
        }

        return checks;
    }

    private static PrerequisiteDirectoryCheck DirectoryCheck(string id, string path, bool ok, string? reason) =>
        new(id, path, ok, reason);

    private static PrerequisiteInstallPlan? BuildInstallPlan(
        string command,
        string manifestSha256,
        IReadOnlyList<PrerequisiteCheckItem> items,
        PrerequisiteServices services)
    {
        if (!services.IsWindows())
        {
            return null;
        }

        PrerequisiteInstallPlanItem[] planItems = items
            .Where(i => i.Level == "hard" && i.Status != "found" && i.Winget is not null)
            .Select(i => new PrerequisiteInstallPlanItem(
                i.Id,
                i.Reason ?? i.Status,
                i.Winget!.PackageId,
                i.Winget.Source,
                i.RequiredVersion))
            .OrderBy(i => i.PrerequisiteId, StringComparer.Ordinal)
            .ToArray();

        if (planItems.Length == 0)
        {
            return null;
        }

        return new PrerequisiteInstallPlan(PlanDigest(command, manifestSha256, planItems), command, planItems);
    }

    private static string PlanDigest(
        string command,
        string manifestSha256,
        IReadOnlyList<PrerequisiteInstallPlanItem> items)
    {
        var builder = new StringBuilder(command).Append('\n')
            .Append("manifest:").Append(manifestSha256).Append('\n');
        foreach (PrerequisiteInstallPlanItem item in items)
        {
            builder.Append(item.PrerequisiteId).Append('|')
                .Append(item.PackageId).Append('|')
                .Append(item.Source).Append('|')
                .Append(item.RequiredVersion).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string FirstOutput(ProcessRunResult result)
    {
        string output = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return string.IsNullOrWhiteSpace(output) ? "exit code " + result.ExitCode : output.Trim();
    }
}
