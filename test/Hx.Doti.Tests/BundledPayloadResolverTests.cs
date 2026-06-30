using Hx.Doti.Core;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 031 T001/T003 (FR-001, D1, SC-001/SC-002): the bundled-payload source resolver. The global tool ships its
/// version-stamped <c>.doti</c> payload beside the executable, so <see cref="BundledPayloadResolver.Resolve(string)"/>
/// returns a base directory iff its <c>.doti/core/skills.json</c> exists (the payload is present), else null so the
/// CLI falls through to the working-directory dev walk. A neutral dir with no <c>.doti</c> resolves null (SC-001 then
/// uses the bundled default at runtime); a dir carrying the payload resolves to itself (SC-002 prefers the bundled
/// payload over any checkout).
/// </summary>
public sealed class BundledPayloadResolverTests
{
    [Fact]
    public void Resolves_a_base_dir_that_carries_the_bundled_payload()
    {
        string baseDir = NewTempDir();
        try
        {
            string skills = Path.Combine(baseDir, ".doti", "core", "skills.json");
            Directory.CreateDirectory(Path.GetDirectoryName(skills)!);
            File.WriteAllText(skills, "{}");

            Assert.Equal(baseDir, BundledPayloadResolver.Resolve(baseDir));
        }
        finally { ForceDelete(baseDir); }
    }

    [Fact]
    public void Returns_null_for_a_neutral_dir_with_no_doti_payload()
    {
        string baseDir = NewTempDir();
        try
        {
            // A bin/-style base directory (a `dotnet run` from source) has no .doti beside it → null, so the CLI
            // falls through to FindDotiSource(CWD).
            Assert.Null(BundledPayloadResolver.Resolve(baseDir));
        }
        finally { ForceDelete(baseDir); }
    }

    [Fact]
    public void Returns_null_for_an_empty_or_whitespace_base_dir()
    {
        Assert.Null(BundledPayloadResolver.Resolve(string.Empty));
        Assert.Null(BundledPayloadResolver.Resolve("   "));
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-resolver-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
