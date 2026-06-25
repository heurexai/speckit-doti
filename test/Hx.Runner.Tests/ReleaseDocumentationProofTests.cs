using Hx.Cycle.Core.Documentation;
using Hx.Gate.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class ReleaseDocumentationProofTests
{
    [Fact]
    public void Documentation_proof_passes_when_readme_and_changelog_cover_release_train()
    {
        using TempDocs repo = TempDocs.Create();
        repo.Write("README.md", "Release 006-task-hash-gated-velopack-completion");
        repo.Write("CHANGELOG.md", "## Next\n- 006-task-hash-gated-velopack-completion");
        repo.Write("docs/notes/operator.md", "Internal operator notes.");

        ReleaseDocumentationProof proof = ReleaseDocumentationInspector.Inspect(repo.Root, Train("006-task-hash-gated-velopack-completion"));

        Assert.Equal(StageOutcome.Pass, proof.Outcome);
        Assert.Contains("006-task-hash-gated-velopack-completion", proof.ReleaseNotes);
        Assert.Contains(proof.Documents, doc => doc.Path == "README.md" && doc.Status == "updated");
        Assert.Contains(proof.Documents, doc => doc.Path == "CHANGELOG.md" && doc.Status == "updated");
        Assert.Contains(proof.Documents, doc => doc.Path == "docs/notes/operator.md" && doc.Status == "no-change");
        Assert.Empty(proof.Blockers);
    }

    [Fact]
    public void Documentation_proof_fails_when_required_release_surface_is_stale()
    {
        using TempDocs repo = TempDocs.Create();
        repo.Write("README.md", "Old CLI behavior.");
        repo.Write("CHANGELOG.md", "006-task-hash-gated-velopack-completion");

        ReleaseDocumentationProof proof = ReleaseDocumentationInspector.Inspect(repo.Root, Train("006-task-hash-gated-velopack-completion"));

        Assert.Equal(StageOutcome.Fail, proof.Outcome);
        Assert.Contains(proof.Documents, doc => doc.Path == "README.md" && doc.Status == "stale");
        Assert.Contains(proof.Blockers, blocker => blocker.Contains("README.md", StringComparison.Ordinal));
        Assert.Contains(proof.Blockers, blocker => blocker.Contains("006-task-hash-gated-velopack-completion", StringComparison.Ordinal));
    }

    [Fact]
    public void Release_documentation_gate_step_blocks_on_stale_docs()
    {
        var proof = new ReleaseDocumentationProof(
            JsonContractDefaults.SchemaVersion,
            StageOutcome.Fail,
            "Release notes\n\n- 006-feature",
            ["006-feature"],
            [new ReleaseDocumentationFileProof("README.md", "stale", "missing release note feature(s): 006-feature")],
            ["README.md: missing release note feature(s): 006-feature"]);

        GateStep step = GateRunner.ReleaseDocumentationStep(proof);

        Assert.Equal(ReleaseDocumentationInspector.StepName, step.Name);
        Assert.Equal(StageOutcome.Fail, step.Outcome);
        Assert.Contains(step.Evidence, evidence => evidence.Message.Contains("README.md", StringComparison.Ordinal));
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
            string root = Path.Combine(Path.GetTempPath(), "hx-doc-proof-" + Guid.NewGuid().ToString("N"));
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
