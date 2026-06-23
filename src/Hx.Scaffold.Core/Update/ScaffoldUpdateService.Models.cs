namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    private sealed record DesiredManagedFile(string Path, string Category, byte[] Content);

    private sealed record ReleasePlan(
        UpdateCacheResult? Cache,
        IReadOnlyList<DesiredManagedFile> Desired,
        string? ExpectedAsset);

    private sealed record MutationResult(
        ScaffoldUpdateWorktreeBackup? BackupWorktree,
        bool Delegated,
        ScaffoldUpdateDelegation? Delegation,
        IReadOnlyList<string> ChangedPaths,
        ScaffoldHookReport? Hook);

    private sealed record ManagedFilePlan(IReadOnlyList<string> CreatePaths, IReadOnlyList<string> ReplacePaths)
    {
        public IReadOnlyList<string> PlannedWritePaths { get; } =
            CreatePaths.Concat(ReplacePaths).OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }
}
