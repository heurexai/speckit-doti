using Hx.Runner.Core.Tools;
using Xunit;

namespace Hx.Runner.Tests;

// 016 (T001, FR-001/003, SC-002): a fetched/stored tool binary must be runnable on Unix; the mode call
// must be a no-op (never throw) on Windows.
public sealed class ExecutableFileModeTests
{
    [Fact]
    public void EnsureExecutable_sets_the_execute_bits_on_unix_and_no_ops_on_windows()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hx-exec-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, [0x7f, 0x45, 0x4c, 0x46]);
        try
        {
            ExecutableFileMode.EnsureExecutable(path); // contract: never throws on any host

            if (OperatingSystem.IsWindows())
            {
                Assert.True(File.Exists(path)); // Windows: a no-op (Unix modes do not apply)
            }
            else
            {
                UnixFileMode mode = File.GetUnixFileMode(path);
                Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
                Assert.True(mode.HasFlag(UnixFileMode.GroupExecute));
                Assert.True(mode.HasFlag(UnixFileMode.OtherExecute));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
