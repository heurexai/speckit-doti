using Hx.Embedding;
using Hx.Embedding.Timing;
using Hx.Semantic;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Semantic.Tests;

/// <summary>
/// FR-018/019/042 (SC-008/SC-022): the service builds code/reference chunks from a <see cref="ChangeSetContext"/> (it
/// does NOT run its own <c>git diff</c>), runs the finder, and reports the ACTIVE engine. Crucially it carries the
/// honesty contract — an empty candidate list is NOT a clean-bill signal (FR-019).
/// </summary>
public sealed class DriftCandidateServiceTests : IDisposable
{
    private readonly string _repo = Path.Combine(
        Path.GetTempPath(), "hx-drift-svc-" + Guid.NewGuid().ToString("N"));

    public DriftCandidateServiceTests() => Directory.CreateDirectory(_repo);

    public void Dispose()
    {
        try { Directory.Delete(_repo, recursive: true); } catch (IOException) { }
    }

    private sealed class TopicEmbedder : IEmbedder
    {
        public string Id => EngineIds.BgeM3;
        public int Dimension => 3;
        public EngineTiming Timing => new(Id, 0, 0, 0);
        public void Dispose() { }
        public float[] Embed(string text, EmbedTask task) =>
            text.Contains("auth", StringComparison.OrdinalIgnoreCase) ? [1f, 0f, 0f] : [0f, 0f, 1f];
    }

    private void Write(string relativePath, string content)
    {
        string full = Path.Combine(_repo, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private ChangeSetContext ChangeSet(params ChangedFile[] files) =>
        new(1, "HEAD~1", "HEAD", "abc123", IncludesWorkingTree: false, RefsResolved: true,
            UnresolvedReason: null, Files: files, AffectedSourceProjects: []);

    [Fact]
    public void Reports_active_engine_chunk_count_and_candidates()
    {
        Write("src/Auth.cs", "auth login token rotation");
        Write("docs/auth.md", "auth login walkthrough");
        using var engine = new EngineSelection(EngineId.BgeM3, new TopicEmbedder());

        DriftCandidatesResult result = new DriftCandidateService().Run(
            _repo,
            ChangeSet(new ChangedFile("src/Auth.cs", ChangeStatus.Modified, null),
                      new ChangedFile("docs/auth.md", ChangeStatus.Modified, null)),
            engine,
            threshold: 0.5);

        Assert.Equal(EngineIds.BgeM3, result.ActiveEngine);            // FR-042
        Assert.Equal(2, result.ChunksEmbedded);                        // one code + one prose
        Assert.NotEmpty(result.Candidates);
        Assert.Equal("docs/auth.md", result.Candidates[0].RelatedPath);
    }

    [Fact]
    public void Absence_of_candidates_is_not_a_clean_bill_signal()
    {
        // Code and prose on orthogonal topics → no candidate. The result must still carry the honesty contract.
        Write("src/Auth.cs", "auth login");
        Write("docs/unrelated.md", "release notes and changelog");
        using var engine = new EngineSelection(EngineId.BgeM3, new TopicEmbedder());

        DriftCandidatesResult result = new DriftCandidateService().Run(
            _repo,
            ChangeSet(new ChangedFile("src/Auth.cs", ChangeStatus.Modified, null),
                      new ChangedFile("docs/unrelated.md", ChangeStatus.Modified, null)),
            engine,
            threshold: 0.5);

        Assert.Empty(result.Candidates);
        Assert.False(string.IsNullOrWhiteSpace(result.AbsenceNote));
        Assert.Contains("NOT a clean-bill", result.AbsenceNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deleted_files_contribute_no_chunks()
    {
        Write("docs/auth.md", "auth login walkthrough");
        using var engine = new EngineSelection(EngineId.BgeM3, new TopicEmbedder());

        DriftCandidatesResult result = new DriftCandidateService().Run(
            _repo,
            ChangeSet(new ChangedFile("src/Gone.cs", ChangeStatus.Deleted, null),
                      new ChangedFile("docs/auth.md", ChangeStatus.Modified, null)),
            engine,
            threshold: 0.5);

        // The deleted .cs reads nothing; only the prose chunk is embedded (no code chunk → no candidate).
        Assert.Equal(1, result.ChunksEmbedded);
        Assert.Empty(result.Candidates);
    }
}
