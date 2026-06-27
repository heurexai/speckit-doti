using Hx.Cycle.Core;
using Hx.Impact.Core.ChangeDetection;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>
/// BL-3 at the identity level: <see cref="ChangeSetIdentity.Compute"/> does no dedup — it trusts the collector's
/// set. The generalised <see cref="ChangeSetParser"/> must reproduce that set exactly (a rename emits only the new
/// path; a case-variant dedups OrdinalIgnoreCase), or every stamped proof's <c>ChangeSetId</c> silently shifts.
/// </summary>
public sealed class ChangeSetIdentityTests
{
    [Fact]
    public void Identity_over_parsed_paths_equals_identity_over_the_expected_deduped_set()
    {
        string dir = NewTempDir();
        try
        {
            Write(dir, "src/new.cs", "renamed-content");
            Write(dir, "src/File.cs", "modified-content");

            // diff: rename old->new + a modified path; status: the same path with a different case (working tree).
            var files = ChangeSetParser.Parse("R100\0src/old.cs\0src/new.cs\0M\0src/File.cs\0", "?? src/file.cs\0");
            string fromParser = ChangeSetIdentity.Compute(dir, files.Select(f => f.Path).ToArray());

            // The expected set: the rename's NEW path only, the case-variant collapsed to the diff's casing.
            string fromExpected = ChangeSetIdentity.Compute(dir, ["src/new.cs", "src/File.cs"]);

            Assert.Equal(fromExpected, fromParser);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Identity_excludes_a_renames_old_path()
    {
        string dir = NewTempDir();
        try
        {
            Write(dir, "src/new.cs", "x");

            var files = ChangeSetParser.Parse("R100\0src/old.cs\0src/new.cs\0", "");
            string fromParser = ChangeSetIdentity.Compute(dir, files.Select(f => f.Path).ToArray());

            // Including the old path would change the identity — proving the old path never participates.
            string withOldPath = ChangeSetIdentity.Compute(dir, ["src/new.cs", "src/old.cs"]);

            Assert.NotEqual(withOldPath, fromParser);
        }
        finally { Directory.Delete(dir, true); }
    }
}
