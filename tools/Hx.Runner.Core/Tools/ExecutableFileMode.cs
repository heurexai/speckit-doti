namespace Hx.Runner.Core.Tools;

/// <summary>
/// 016 (FR-001/002/003): mark a written tool binary executable on Unix. A binary fetched (<see cref="ToolFetcher"/>)
/// or store-populated (<c>StorePopulator</c>) lands at the default <c>0644</c> (no execute), so on Linux/macOS the
/// hash-verified binary cannot run ("Permission denied"). This sets the owner/group/other execute bits; it is a
/// no-op on Windows (where <see cref="File.SetUnixFileMode(string, UnixFileMode)"/> throws, and the mode is
/// irrelevant). Only the bytes' MODE changes — the already-verified content is untouched (no re-hash).
/// </summary>
public static class ExecutableFileMode
{
    private const UnixFileMode ReadWriteExecuteOwnerReadExecuteOthers =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute; // 0755

    /// <summary>Set <c>0755</c> on <paramref name="path"/> on non-Windows hosts; a no-op on Windows.</summary>
    public static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, ReadWriteExecuteOwnerReadExecuteOthers);
    }
}
