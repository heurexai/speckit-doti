using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

public sealed record DotiInstallBootstrapRequest(
    string? TargetRepoRoot,
    IReadOnlyList<DotiAgentTarget> Agents,
    string? RepositoryName = null,
    bool Force = false);

/// <summary>
/// Explicit-target bootstrap entrypoint for installer hosts. CLI and future Velopack hosts call this
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

        return DotiInstaller.Install(sourceRepoRoot, target, request.Agents, repoName, request.Force);
    }
}
