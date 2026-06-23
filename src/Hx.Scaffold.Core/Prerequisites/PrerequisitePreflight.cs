using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Scaffold.Core.Prerequisites;

public static class PrerequisitePreflight
{
    public static PrerequisiteCheckReport Check(
        PrerequisiteCheckRequest request,
        PrerequisiteServices? services = null)
    {
        services ??= new PrerequisiteServices();
        LoadedPrerequisiteManifest loaded = PrerequisiteManifestStore.LoadFromSourceRoot(request.SourceRoot);
        var items = new List<PrerequisiteCheckItem>();
        var blockers = new List<string>();
        var nextActions = new List<string>();

        foreach (PrerequisiteRequirement requirement in loaded.Manifest.Requirements)
        {
            string? level = LevelFor(requirement, request.Command);
            if (level is null)
            {
                continue;
            }

            PrerequisiteCheckItem item = Probe(requirement, level, request, services);
            items.Add(item);
            if (level == "hard" && item.Status != "found")
            {
                blockers.Add($"{requirement.DisplayName}: {item.Reason ?? item.Status}");
                foreach (string instruction in requirement.Instructions)
                {
                    if (!nextActions.Contains(instruction, StringComparer.Ordinal))
                    {
                        nextActions.Add(instruction);
                    }
                }
            }
        }

        IReadOnlyList<PrerequisiteDirectoryCheck> directories = CheckDirectories(request);
        foreach (PrerequisiteDirectoryCheck directory in directories.Where(d => !d.Ok))
        {
            blockers.Add($"{directory.Id}: {directory.Reason}");
        }

        PrerequisiteInstallPlan? plan = BuildInstallPlan(request.Command, loaded.Sha256, items, services);
        return new PrerequisiteCheckReport(
            JsonContractDefaults.SchemaVersion,
            request.Command,
            loaded.Path,
            loaded.Sha256,
            blockers.Count == 0,
            items,
            directories,
            blockers,
            nextActions,
            plan,
            []);
    }

    public static PrerequisiteCheckReport Install(
        PrerequisiteCheckRequest request,
        string? confirmPlan,
        PrerequisiteServices? services = null)
    {
        services ??= new PrerequisiteServices();
        PrerequisiteCheckReport initial = Check(request, services);
        if (initial.Ok)
        {
            return initial;
        }

        if (!services.IsWindows())
        {
            return WithBlocker(initial, "automatic prerequisite installation is supported only on Windows");
        }

        if (initial.InstallPlan is null)
        {
            return WithBlocker(initial, "no trusted winget package mapping is available for the missing prerequisite");
        }

        if (!string.Equals(confirmPlan, initial.InstallPlan.Digest, StringComparison.Ordinal))
        {
            return WithBlocker(initial, "operator approval is required for prerequisite install plan " + initial.InstallPlan.Digest);
        }

        ProcessRunResult wingetProbe;
        try
        {
            wingetProbe = services.RunProcess("winget", ["--version"], request.SourceRoot);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return WithBlocker(initial, "winget is unavailable: " + ex.Message);
        }

        if (wingetProbe.ExitCode != 0)
        {
            return WithBlocker(initial, "winget is unavailable: " + FirstOutput(wingetProbe));
        }

        var executions = new List<PrerequisiteInstallExecution>();
        foreach (PrerequisiteInstallPlanItem item in initial.InstallPlan.Items)
        {
            ProcessRunResult result = services.RunProcess(
                "winget",
                ["install", "--id", item.PackageId, "--exact", "--source", item.Source,
                    "--accept-package-agreements", "--accept-source-agreements"],
                request.SourceRoot);
            executions.Add(new PrerequisiteInstallExecution(
                item.PrerequisiteId,
                item.PackageId,
                item.Source,
                result.ExitCode,
                result.StandardOutput,
                result.StandardError));
            if (result.ExitCode != 0)
            {
                return initial with
                {
                    Blockers = initial.Blockers.Concat([$"winget failed for {item.PrerequisiteId}: {FirstOutput(result)}"]).ToArray(),
                    InstallExecutions = executions,
                    Ok = false,
                };
            }
        }

        PrerequisiteCheckReport verified = Check(request, services);
        return verified with { InstallExecutions = executions };
    }

    private static PrerequisiteCheckReport WithBlocker(PrerequisiteCheckReport report, string blocker) =>
        report with { Ok = false, Blockers = report.Blockers.Concat([blocker]).ToArray() };

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

        if ((request.Command == PrerequisiteCommands.Update || request.Command == PrerequisiteCommands.Version)
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
