using Hx.Embedding.Results;

namespace Hx.Embedding;

/// <summary>
/// Pure similarity math over embedding vectors — engine-agnostic, no IO (FR-004). The vectors are produced by an
/// <see cref="IEmbedder"/>; this service only compares them. Deterministic in input order.
/// </summary>
public sealed class SimilarityService
{
    /// <summary>Cosine of two vectors (clamped to [-1,1]; zero vector → 0).</summary>
    public double Cosine(float[] a, float[] b) => Vectors.Cosine(a, b);

    /// <summary>The full pairwise cosine matrix for an item set (single engine).</summary>
    public PairMatrix Matrix(IReadOnlyList<string> items, IReadOnlyList<float[]> vectors)
    {
        RequireAligned(items, vectors);
        int n = vectors.Count;
        var m = new double[n][];
        for (int i = 0; i < n; i++)
        {
            m[i] = new double[n];
            for (int j = 0; j < n; j++)
            {
                m[i][j] = Vectors.Cosine(vectors[i], vectors[j]);
            }
        }

        return new PairMatrix(items, m);
    }

    /// <summary>
    /// Single-link greedy clustering: any item within <paramref name="threshold"/> cosine (inclusive,
    /// <c>&gt;=</c>) of a current cluster member joins that cluster. Deterministic (input order); every item lands
    /// in exactly one cluster (singletons included).
    /// </summary>
    public IReadOnlyList<Cluster> Dedupe(IReadOnlyList<string> items, IReadOnlyList<float[]> vectors, double threshold)
    {
        RequireAligned(items, vectors);
        int n = vectors.Count;
        var assigned = new int[n];
        Array.Fill(assigned, -1);
        var clusters = new List<List<int>>();

        for (int i = 0; i < n; i++)
        {
            if (assigned[i] >= 0)
            {
                continue;
            }

            int id = clusters.Count;
            var members = new List<int> { i };
            assigned[i] = id;
            for (int k = 0; k < members.Count; k++) // BFS over the growing member set (single-link)
            {
                int a = members[k];
                for (int j = 0; j < n; j++)
                {
                    if (assigned[j] >= 0)
                    {
                        continue;
                    }

                    if (Vectors.Cosine(vectors[a], vectors[j]) >= threshold)
                    {
                        assigned[j] = id;
                        members.Add(j);
                    }
                }
            }

            clusters.Add(members);
        }

        return clusters
            .Select(c => new Cluster(c, c.Select(idx => items[idx]).ToArray()))
            .ToArray();
    }

    /// <summary>Cross-engine agreement for one pair: each engine's cosine + the absolute divergence.</summary>
    public EngineAgreement Agreement(string engineA, double cosineA, string engineB, double cosineB) =>
        new(engineA, cosineA, engineB, cosineB, Math.Abs(cosineA - cosineB));

    private static void RequireAligned(IReadOnlyList<string> items, IReadOnlyList<float[]> vectors)
    {
        if (items.Count != vectors.Count)
        {
            throw new SemanticException($"items/vectors count mismatch: {items.Count} vs {vectors.Count}.");
        }
    }
}
