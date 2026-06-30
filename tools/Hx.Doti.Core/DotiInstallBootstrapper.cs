using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core;

public sealed record DotiInstallBootstrapRequest(
    string? TargetRepoRoot,
    IReadOnlyList<DotiAgentTarget> Agents,
    string? RepositoryName = null,
    bool Force = false,
    // 029 D8/FR-008: additive trailing-optional — existing callers compile unchanged. Carries the resolved
    // Install-subset setup config so DotiInstaller can project the doti-layer fields; null on the no-config path
    // (SC-007: byte-identical; also guards doti update/update-all which never pass it).
    ResolvedSetupConfig? Setup = null);

/// <summary>
/// Explicit-target bootstrap entrypoint for installer hosts. CLI and channel installer hosts call this
/// instead of guessing a current directory.
/// </summary>
public static class DotiInstallBootstrapper
{
    public static DotiInstallResult Install(string sourceRepoRoot, DotiInstallBootstrapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetRepoRoot))
        {
            throw new ArgumentException("Doti install requires an explicit target repository directory.", nameof(request));
        }

        string target = Path.GetFullPath(request.TargetRepoRoot);
        string repoName = string.IsNullOrWhiteSpace(request.RepositoryName)
            ? Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : request.RepositoryName!;

        return DotiInstaller.Install(sourceRepoRoot, target, request.Agents, repoName, request.Force, setup: request.Setup);
    }
}
