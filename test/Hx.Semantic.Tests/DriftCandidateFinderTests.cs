using Hx.Embedding;
using Hx.Embedding.Timing;
using Hx.Semantic;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Semantic.Tests;

/// <summary>
/// FR-018/019 (SC-008): the advisory finder embeds changed-code and reference (prose) chunks and surfaces cross-category
/// pairs that sit ABOVE a cosine threshold — a recall-favouring "look here" signal, never a verdict. Driven by a
/// deterministic topic-vector stub so the ranking is provable without a real model.
/// </summary>
public sealed class DriftCandidateFinderTests
{
    /// <summary>Maps text to a one-hot topic vector (auth / billing / other), so a code chunk and a prose chunk on the
    /// same topic embed identically (cosine 1.0) and different topics are orthogonal (cosine 0.0).</summary>
    private sealed class TopicEmbedder : IEmbedder
    {
        public string Id => EngineIds.BgeM3;
        public int Dimension => 3;
        public EngineTiming Timing => new(Id, 0, 0, 0);
        public void Dispose() { }

        public float[] Embed(string text, EmbedTask task)
        {
            if (text.Contains("auth", StringComparison.OrdinalIgnoreCase)) return [1f, 0f, 0f];
            if (text.Contains("billing", StringComparison.OrdinalIgnoreCase)) return [0f, 1f, 0f];
            return [0f, 0f, 1f];
        }
    }

    [Fact]
    public void Emits_a_candidate_for_a_close_cross_category_pair()
    {
        var finder = new DriftCandidateFinder(new TopicEmbedder());
        IReadOnlyList<DriftChunk> code = [new("src/Auth.cs", "runtime-code", "auth login token rotation")];
        IReadOnlyList<DriftChunk> reference =
        [
            new("docs/auth.md", "prose", "auth login walkthrough"),
            new("docs/billing.md", "prose", "billing invoices and dunning"),
        ];

        IReadOnlyList<SemanticCandidate> candidates = finder.Find(code, reference, threshold: 0.5);

        SemanticCandidate candidate = Assert.Single(candidates);
        Assert.Equal("docs/auth.md", candidate.RelatedPath); // the auth doc, not the orthogonal billing doc
        Assert.Equal(1.0, candidate.Confidence, 3);
        Assert.Equal(["runtime-code<->prose"], candidate.AffectedAxes);
        Assert.NotEmpty(candidate.SuggestedDeterministicChecks); // points at a deterministic re-check, never a verdict
        Assert.Contains("auth", candidate.EvidenceSnippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Drops_pairs_below_the_threshold()
    {
        var finder = new DriftCandidateFinder(new TopicEmbedder());
        IReadOnlyList<DriftChunk> code = [new("src/Auth.cs", "runtime-code", "auth login")];
        IReadOnlyList<DriftChunk> reference = [new("docs/billing.md", "prose", "billing invoices")];

        Assert.Empty(finder.Find(code, reference, threshold: 0.5)); // orthogonal topics → no candidate
    }

    [Fact]
    public void Empty_inputs_yield_no_candidates()
    {
        var finder = new DriftCandidateFinder(new TopicEmbedder());
        Assert.Empty(finder.Find([], [new("docs/a.md", "prose", "auth")], threshold: 0.5));
        Assert.Empty(finder.Find([new("a.cs", "runtime-code", "auth")], [], threshold: 0.5));
    }
}
