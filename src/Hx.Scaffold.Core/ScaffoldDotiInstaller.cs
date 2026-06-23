using Hx.Doti.Core;
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
        DotiInstaller.Install(sourceRepoRoot, targetRoot, agents, request.Name);
        ScaffoldVersionReporter.WriteStamp(targetRoot,
            ScaffoldVersionReporter.IdentityFromVersion(scaffoldVersion ?? "0.0.0", "hx-scaffold new"));
    }
}
