using Hx.Doti.Core;
using Hx.Scaffold.Core.Release;
using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

internal static class ScaffoldDotiInstaller
{
    public static void Install(
        string sourceRepoRoot,
        string targetRoot,
        ScaffoldRequest request,
        string? scaffoldVersion)
    {
        DotiAgentTarget[] agents = request.Agents
            .Select(DotiAgentTarget.FromKey)
            .Where(a => a is not null)
            .Cast<DotiAgentTarget>()
            .ToArray();
        // 009 FR-015: the explicit --name is the constitution title for a generated repo (it wins over solution/dir).
        DotiInstaller.Install(sourceRepoRoot, targetRoot, agents, request.Name, projectNameOverride: request.Name);
        ScaffoldReleaseTargetWriter.WriteDefault(targetRoot, request.Name);
        ScaffoldVersionReporter.WriteStamp(targetRoot,
            ScaffoldVersionReporter.IdentityFromVersion(scaffoldVersion ?? "0.0.0", "hx-scaffold new"));
    }
}
