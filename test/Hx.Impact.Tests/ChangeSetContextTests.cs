using Hx.Impact.Core.ChangeDetection;
using Hx.Impact.Core.Domain;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Impact.Tests;

public sealed class ChangeSetContextTests
{
    // A canned git seam so the builder/collector are unit-testable without a real repository.
    private sealed class FakeGitSource(GitChangeOutputs outputs, bool? expectWorkingTree = null) : IGitChangeSource
    {
        public bool? LastIncludeWorkingTree { get; private set; }

        public GitChangeOutputs Read(string repositoryRoot, string baseRef, string headRef, bool includeWorkingTree)
        {
            LastIncludeWorkingTree = includeWorkingTree;
            if (expectWorkingTree is { } expected)
            {
                Assert.Equal(expected, includeWorkingTree);
            }

            return outputs;
        }
    }

    private static string DiffZ(params string[] tokens) => string.Concat(tokens.Select(t => t + "\0"));

    [Fact]
    public void Parse_captures_status_rename_and_delete_metadata()
    {
        string diff = DiffZ("M", "src/a.cs", "D", "src/b.cs", "R100", "src/old.cs", "src/new.cs", "A", "src/c.cs");

        IReadOnlyList<ChangedFile> files = ChangeSetParser.Parse(diff, "");

        Assert.Equal(new[] { "src/a.cs", "src/b.cs", "src/c.cs", "src/new.cs" }, files.Select(f => f.Path));
        Assert.Equal(ChangeStatus.Modified, files.Single(f => f.Path == "src/a.cs").Status);
        Assert.Equal(ChangeStatus.Deleted, files.Single(f => f.Path == "src/b.cs").Status);
        Assert.Equal(ChangeStatus.Added, files.Single(f => f.Path == "src/c.cs").Status);
        ChangedFile renamed = files.Single(f => f.Path == "src/new.cs");
        Assert.Equal(ChangeStatus.Renamed, renamed.Status);
        Assert.Equal("src/old.cs", renamed.OldPath);
    }

    [Fact]
    public void Parse_rename_collapses_to_the_new_path_only()
    {
        // BL-3: a rename must emit ONLY the new path (old path is metadata), so the change-set-identity set is
        // unchanged from the pre-generalisation collector.
        IReadOnlyList<ChangedFile> files = ChangeSetParser.Parse(DiffZ("R100", "src/old.cs", "src/new.cs"), "");

        Assert.Equal(new[] { "src/new.cs" }, files.Select(f => f.Path));
        Assert.DoesNotContain(files, f => f.Path == "src/old.cs");
    }

    [Fact]
    public void Parse_dedups_case_variant_paths_OrdinalIgnoreCase_diff_winning()
    {
        // BL-3: the committed diff is unioned before the working tree; a case-variant collapses to one entry.
        IReadOnlyList<ChangedFile> files = ChangeSetParser.Parse(DiffZ("M", "src/File.cs"), "?? src/file.cs\0");

        ChangedFile only = Assert.Single(files);
        Assert.Equal("src/File.cs", only.Path); // diff casing wins (first-seen)
        Assert.Equal(ChangeStatus.Modified, only.Status);
    }

    [Fact]
    public void Parse_unions_committed_diff_with_the_working_tree()
    {
        string diff = DiffZ("M", "src/a.cs");
        string status = "?? docs/new.md\0 D src/gone.cs\0";

        IReadOnlyList<ChangedFile> files = ChangeSetParser.Parse(diff, status);

        Assert.Equal(new[] { "docs/new.md", "src/a.cs", "src/gone.cs" }, files.Select(f => f.Path));
        Assert.Equal(ChangeStatus.Untracked, files.Single(f => f.Path == "docs/new.md").Status);
    }

    [Fact]
    public void Build_carries_base_sha_working_tree_flag_and_files()
    {
        var source = new FakeGitSource(
            new GitChangeOutputs(true, "abc123", null, DiffZ("M", "src/a.cs"), ""), expectWorkingTree: true);

        ChangeSetContext context = new ChangeSetContextBuilder(source).Build("/repo", "base", "HEAD");

        Assert.True(context.RefsResolved);
        Assert.Equal("abc123", context.BaseSha);
        Assert.True(context.IncludesWorkingTree);
        Assert.Equal(new[] { "src/a.cs" }, context.Files.Select(f => f.Path));
        Assert.Empty(context.AffectedSourceProjects); // no graph supplied
    }

    [Fact]
    public void Build_resolves_affected_source_projects_when_a_graph_is_supplied()
    {
        var source = new FakeGitSource(new GitChangeOutputs(true, "sha", null, DiffZ("M", "src/A/Widget.cs"), ""));

        ChangeSetContext context = new ChangeSetContextBuilder(source).Build("/repo", "base", "HEAD", graph: SampleGraph());

        Assert.Contains("A", context.AffectedSourceProjects);
    }

    [Fact]
    public void Build_excludes_the_working_tree_for_a_historical_diff()
    {
        // H-1: the release-train pairwise comparison passes includeWorkingTree:false so in-flight edits are not
        // attributed to the later feature.
        var source = new FakeGitSource(
            new GitChangeOutputs(true, "sha", null, DiffZ("M", "src/a.cs"), ""), expectWorkingTree: false);

        ChangeSetContext context = new ChangeSetContextBuilder(source).Build("/repo", "earlier", "later", includeWorkingTree: false);

        Assert.False(context.IncludesWorkingTree);
        Assert.False(source.LastIncludeWorkingTree!.Value);
    }

    [Fact]
    public void Build_fails_closed_with_RefsResolved_false_on_unresolved_merge_base()
    {
        var source = new FakeGitSource(new GitChangeOutputs(false, null, "no merge-base", "", ""));

        ChangeSetContext context = new ChangeSetContextBuilder(source).Build("/repo", "ghost", "HEAD");

        Assert.False(context.RefsResolved);
        Assert.Equal("no merge-base", context.UnresolvedReason);
        Assert.Empty(context.Files);
    }

    [Fact]
    public void Collector_throws_fail_closed_on_unresolved_merge_base()
    {
        // The change-set-identity path must never compute over a misleading set.
        var source = new FakeGitSource(new GitChangeOutputs(false, null, "no merge-base", "", ""));

        Assert.Throws<InvalidOperationException>(
            () => new ImpactChangeCollector(source).Collect("/repo", "ghost", "HEAD"));
    }

    [Fact]
    public void Collector_path_set_matches_the_parser_path_set()
    {
        // The bare-path collector is a thin adapter over the same parse the rich builder uses (BL-3).
        string diff = DiffZ("M", "src/a.cs", "R100", "src/old.cs", "src/new.cs");
        string status = "?? docs/x.md\0";
        var source = new FakeGitSource(new GitChangeOutputs(true, "sha", null, diff, status));

        IReadOnlyList<string> collected = new ImpactChangeCollector(source).Collect("/repo", "base", "HEAD");

        Assert.Equal(ChangeSetParser.Parse(diff, status).Select(f => f.Path), collected);
    }

    // src/A <- src/B ; A.Tests covers A. (mirrors AffectedTestPlannerTests' fixture)
    private static ProjectGraph SampleGraph()
    {
        (string Path, bool IsTest, string[] Refs)[] defs =
        [
            ("src/A/A.csproj", false, []),
            ("test/A.Tests/A.Tests.csproj", true, ["src/A/A.csproj"]),
        ];

        var nodes = defs.ToDictionary(
            d => d.Path,
            d => new ProjectNode(d.Path, Path.GetFileNameWithoutExtension(d.Path), d.IsTest, d.Refs),
            StringComparer.OrdinalIgnoreCase);
        var edges = defs.ToDictionary(d => d.Path, d => (IReadOnlyList<string>)d.Refs, StringComparer.OrdinalIgnoreCase);
        var reverse = defs.ToDictionary(d => d.Path, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach ((string path, _, string[] refs) in defs)
        {
            foreach (string reference in refs)
            {
                reverse[reference].Add(path);
            }
        }

        var reverseReadonly = reverse.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        return new ProjectGraph(nodes, edges, reverseReadonly, []);
    }
}
