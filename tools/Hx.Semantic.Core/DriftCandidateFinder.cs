using Hx.Embedding;
using Hx.Tooling.Contracts;

namespace Hx.Semantic;

/// <summary>A chunk of changed/reference text to compare: its path, a category label, and the text.</summary>
public sealed record DriftChunk(string Path, string Category, string Text);

/// <summary>
/// FR-018/019 (advisory): find SEMANTIC drift candidates — a changed-code chunk that embeds CLOSE to a doc/skill/help
/// section, suggesting that prose may no longer match the behaviour the code now has (a drift the deterministic
/// Axis-2 grep can miss when there is no literal symbol match). Pure orchestration over an injected
/// <see cref="IEmbedder"/> (engine-agnostic): embed both sides, rank cross-category pairs above a cosine threshold.
/// Recall-favouring — a candidate is a spot worth a human/deterministic look, never a verdict; NEVER gating.
/// </summary>
public sealed class DriftCandidateFinder
{
    private readonly IEmbedder _embedder;
    private readonly SimilarityService _similarity = new();

    public DriftCandidateFinder(IEmbedder embedder) => _embedder = embedder;

    public IReadOnlyList<SemanticCandidate> Find(
        IReadOnlyList<DriftChunk> changedCode,
        IReadOnlyList<DriftChunk> reference,
        double threshold,
        int topN = 20,
        EmbedTask? task = null)
    {
        if (changedCode.Count == 0 || reference.Count == 0)
        {
            return [];
        }

        // SYMMETRIC by default; the service passes SymmetricInstructed so Qwen3 (only) carries a code/.NET instruction
        // on BOTH sides — symmetry preserved (FR-013/FR-015). BGE-M3 ignores the task and stays instruction-free.
        EmbedTask embedTask = task ?? EmbedTask.Symmetric;
        float[][] codeVectors = changedCode.Select(c => _embedder.Embed(c.Text, embedTask)).ToArray();
        float[][] referenceVectors = reference.Select(r => _embedder.Embed(r.Text, embedTask)).ToArray();

        var candidates = new List<SemanticCandidate>();
        for (int i = 0; i < changedCode.Count; i++)
        {
            int best = -1;
            double bestCosine = threshold;
            for (int j = 0; j < reference.Count; j++)
            {
                double cosine = _similarity.Cosine(codeVectors[i], referenceVectors[j]);
                if (cosine >= bestCosine)
                {
                    bestCosine = cosine;
                    best = j;
                }
            }

            if (best >= 0)
            {
                candidates.Add(new SemanticCandidate(
                    Snippet(changedCode[i].Text),
                    Math.Round(bestCosine, 4),
                    reference[best].Path,
                    [$"{changedCode[i].Category}<->{reference[best].Category}"],
                    [
                        $"grep the changed symbol(s) against {reference[best].Path}",
                        "confirm the deterministic drift-review axes (spec<->code, code<->docs) for this pair",
                    ]));
            }
        }

        return candidates
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.RelatedPath, StringComparer.Ordinal)
            .Take(topN)
            .ToList();
    }

    private static string Snippet(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length <= 240 ? trimmed : trimmed[..240] + "…";
    }
}
