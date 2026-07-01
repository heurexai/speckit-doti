using Hx.Cycle.Core.Documentation;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>
/// Bug 035 Fix G: <see cref="ReleaseDocumentationInspector"/> matches a release-train member's feature slug as a
/// BOUNDED TOKEN, not a raw substring. A raw <c>text.Contains(slug, OrdinalIgnoreCase)</c> false-positives — a short
/// or numeric-prefixed slug like <c>032-x</c> is satisfied by an unrelated <c>1032-x</c> elsewhere in the doc.
/// </summary>
public sealed class ReleaseDocumentationSlugMatchTests
{
    [Fact]
    public void An_unrelated_longer_numeric_prefixed_slug_does_not_satisfy_the_member_slug()
    {
        using TempDocs repo = TempDocs.Create();
        // README/CHANGELOG mention only the UNRELATED "1032-x" — never "032-x" as its own token.
        repo.Write("README.md", "See 1032-x for details.");
        repo.Write("CHANGELOG.md", "- 1032-x shipped");

        ReleaseDocumentationProof proof = ReleaseDocumentationInspector.Inspect(repo.Root, Train("032-x"));

        Assert.Equal(StageOutcome.Fail, proof.Outcome);
        Assert.Contains(proof.Documents, doc => doc.Path == "README.md" && doc.Status == "stale");
        Assert.Contains(proof.Blockers, blocker => blocker.Contains("032-x", StringComparison.Ordinal));
    }

    [Fact]
    public void The_slug_as_a_bullet_heading_satisfies_the_match()
    {
        using TempDocs repo = TempDocs.Create();
        repo.Write("README.md", "## Release notes\n\n- **032-foo** shipped this cycle.\n");
        repo.Write("CHANGELOG.md", "## Unreleased\n\n- **032-foo** shipped this cycle.\n");

        ReleaseDocumentationProof proof = ReleaseDocumentationInspector.Inspect(repo.Root, Train("032-foo"));

        Assert.Equal(StageOutcome.Pass, proof.Outcome);
        Assert.Contains(proof.Documents, doc => doc.Path == "README.md" && doc.Status == "updated");
        Assert.Contains(proof.Documents, doc => doc.Path == "CHANGELOG.md" && doc.Status == "updated");
        Assert.Empty(proof.Blockers);
    }

    [Fact]
    public void The_slug_as_a_standalone_token_satisfies_the_match()
    {
        using TempDocs repo = TempDocs.Create();
        repo.Write("README.md", "032-foo");
        repo.Write("CHANGELOG.md", "032-foo");

        ReleaseDocumentationProof proof = ReleaseDocumentationInspector.Inspect(repo.Root, Train("032-foo"));

        Assert.Equal(StageOutcome.Pass, proof.Outcome);
        Assert.Empty(proof.Blockers);
    }

    [Fact]
    public void The_slug_mid_sentence_satisfies_the_match()
    {
        using TempDocs repo = TempDocs.Create();
        repo.Write("README.md", "This release includes 032-foo which fixes the worktree leak.");
        repo.Write("CHANGELOG.md", "Includes 032-foo (worktree leak fix).");

        ReleaseDocumentationProof proof = ReleaseDocumentationInspector.Inspect(repo.Root, Train("032-foo"));

        Assert.Equal(StageOutcome.Pass, proof.Outcome);
        Assert.Empty(proof.Blockers);
    }

    private static CycleReleaseTrain Train(string feature) => new(
        JsonContractDefaults.SchemaVersion,
        Valid: true,
        Features:
        [
            new CycleReleaseTrainFeature(
                feature,
                "drift-review",
                "abc123",
                "base..abc123",
                "pass",
                "pass",
                "included",
                [])
        ],
        Blockers: []);

    private sealed class TempDocs : IDisposable
    {
        private TempDocs(string root) => Root = root;

        public string Root { get; }

        public static TempDocs Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "hx-doc-slug-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempDocs(root);
        }

        public void Write(string relativePath, string text)
        {
            string full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
