using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Prerequisites;

public static partial class PrerequisitePreflight
{
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
}
