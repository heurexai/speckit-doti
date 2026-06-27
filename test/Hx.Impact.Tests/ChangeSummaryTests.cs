using Hx.Impact.Core.ChangeDetection;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Impact.Tests;

/// <summary>
/// 012 (T003): the <see cref="ChangeSummaryProjector"/> categorizes changed files (source/test/docs/other), sums
/// lines ± from numstat, scans changed <c>.cs</c> for top-level types (lexer-aware), orders deterministically, and
/// caps lists with "+N more" (FR-013/018/020). The class scan is the load-bearing safety: a <c>class</c> token in a
/// string or comment must never be counted.
/// </summary>
public sealed class ChangeSummaryTests
{
    private sealed class FakeNumstat(params NumstatEntry[] entries) : INumstatReader
    {
        public IReadOnlyList<NumstatEntry> Read(string repositoryRoot, string baseRef, string headRef) => entries;
    }

    private static ChangeSetContext Context(params (string Path, ChangeStatus Status)[] files) =>
        new(JsonContractDefaults.SchemaVersion, "base", "HEAD", "sha", true, true, null,
            files.Select(f => new ChangedFile(f.Path, f.Status, null)).ToArray(), []);

    [Fact]
    public void Categorizes_source_test_docs_and_other()
    {
        ChangeSetContext context = Context(
            ("src/A/Widget.cs", ChangeStatus.Modified),
            ("tools/Hx.X/Hx.X.csproj", ChangeStatus.Modified),
            ("test/A.Tests/WidgetTests.cs", ChangeStatus.Added),
            ("docs/specs/feature.md", ChangeStatus.Added),
            ("README.md", ChangeStatus.Modified),
            ("build.ps1", ChangeStatus.Modified));

        ChangeSummary summary = ChangeSummaryProjector.Build("/repo", context, [], includeClasses: false);

        Assert.Equal(2, summary.Source); // src/.cs + tools/.csproj
        Assert.Equal(1, summary.Test);   // under test/
        Assert.Equal(2, summary.Docs);   // docs/ + .md
        Assert.Equal(1, summary.Other);  // build.ps1
        Assert.False(summary.ClassesIncluded);
        Assert.Empty(summary.ClassesTouched);
    }

    [Fact]
    public void Sums_lines_added_and_removed_from_numstat()
    {
        ChangeSetContext context = Context(("src/A/Widget.cs", ChangeStatus.Modified));
        var numstat = new[] { new NumstatEntry(10, 3, "src/A/Widget.cs"), new NumstatEntry(2, 0, "src/A/Other.cs") };

        ChangeSummary summary = ChangeSummaryProjector.Build("/repo", context, numstat, includeClasses: false);

        Assert.Equal(12, summary.LinesAdded);
        Assert.Equal(3, summary.LinesRemoved);
    }

    [Fact]
    public void Files_are_ordinally_ordered_and_capped_with_overflow_marker()
    {
        var files = Enumerable.Range(0, ChangeSummaryProjector.ListCap + 5)
            .Select(i => ($"src/A/File{i:D2}.cs", ChangeStatus.Modified))
            .ToArray();
        ChangeSummary summary = ChangeSummaryProjector.Build("/repo", Context(files), [], includeClasses: false);

        Assert.Equal(ChangeSummaryProjector.ListCap + 1, summary.Files.Count); // cap + the "+N more" marker
        Assert.EndsWith(" more", summary.Files[^1]);
        Assert.Equal(summary.Files.Where(f => !f.EndsWith("more")).OrderBy(f => f, StringComparer.Ordinal),
            summary.Files.Where(f => !f.EndsWith("more")));
    }

    [Fact]
    public void Numstat_parse_unions_committed_and_working_and_maps_binary_to_zero()
    {
        IReadOnlyList<NumstatEntry> entries = ChangeSummaryProjector.ParseNumstat(
            "10\t2\tsrc/a.cs\n-\t-\tassets/logo.png\n",
            "5\t0\tsrc/b.cs\n10\t9\tsrc/a.cs\n"); // src/a.cs first-seen (committed) wins

        Assert.Equal(3, entries.Count);
        Assert.Equal(2, entries.Single(e => e.Path == "src/a.cs").Removed); // committed value, not the working 9
        Assert.Equal(0, entries.Single(e => e.Path == "assets/logo.png").Added); // binary → 0
    }

    [Fact]
    public void ClassesTouched_scans_top_level_types_lexer_aware()
    {
        using var repo = new TempRepo();
        repo.Write("src/A/Types.cs", """
            namespace A;
            // class Commented should be ignored
            public sealed class Widget
            {
                public string Note => "class StringLiteral is not a type";
                private struct Inner { } // nested — not top-level
            }
            public record Gadget(int X);
            public interface IThing { }
            public enum Color { Red }
            """);

        IReadOnlyList<string> classes = ChangeSummaryProjector.ClassesTouched(repo.Path, ["src/A/Types.cs"]);

        Assert.Contains("Widget", classes);
        Assert.Contains("Gadget", classes);
        Assert.Contains("IThing", classes);
        Assert.Contains("Color", classes);
        Assert.DoesNotContain("Commented", classes);     // in a comment
        Assert.DoesNotContain("StringLiteral", classes);  // in a string
        Assert.DoesNotContain("Inner", classes);          // nested type
        // Deterministic Ordinal ordering.
        Assert.Equal(classes.OrderBy(c => c, StringComparer.Ordinal), classes);
    }

    [Fact]
    public void ClassesTouched_skips_missing_files_without_throwing()
    {
        using var repo = new TempRepo();
        IReadOnlyList<string> classes = ChangeSummaryProjector.ClassesTouched(repo.Path, ["src/A/Gone.cs"]);
        Assert.Empty(classes);
    }

    [Fact]
    public void Build_includes_classes_only_when_requested()
    {
        using var repo = new TempRepo();
        repo.Write("src/A/Widget.cs", "namespace A; public class Widget { }");
        ChangeSetContext context = Context(("src/A/Widget.cs", ChangeStatus.Modified));

        ChangeSummary basic = ChangeSummaryProjector.Build(repo.Path, context, [], includeClasses: false);
        ChangeSummary detailed = ChangeSummaryProjector.Build(repo.Path, context, [], includeClasses: true);

        Assert.Empty(basic.ClassesTouched);
        Assert.False(basic.ClassesIncluded);
        Assert.Equal(["Widget"], detailed.ClassesTouched);
        Assert.True(detailed.ClassesIncluded);
    }

    private sealed class TempRepo : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "hx-change-summary-" + Guid.NewGuid().ToString("N"));

        public TempRepo() => Directory.CreateDirectory(Path);

        public void Write(string relative, string content)
        {
            string full = System.IO.Path.Combine(Path, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }
}
