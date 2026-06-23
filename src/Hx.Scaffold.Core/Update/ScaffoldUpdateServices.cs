namespace Hx.Scaffold.Core.Update;

public sealed class ScaffoldUpdateServices
{
    public Func<string, UpdateRelease> ResolveLatest { get; init; } = GitHubReleaseResolver.ResolveLatest;
    public Func<Uri, byte[]> DownloadBytes { get; init; } = GitHubReleaseResolver.DownloadBytes;
    public Func<string> CacheRoot { get; init; } = DefaultCacheRoot;
    public Func<string> WorktreeRoot { get; init; } = DefaultWorktreeRoot;

    private static string DefaultCacheRoot() =>
        Path.Combine(Path.GetTempPath(), "speckit-doti-update-cache");

    private static string DefaultWorktreeRoot() =>
        Path.Combine(Path.GetTempPath(), "speckit-doti-update-worktrees");
}
