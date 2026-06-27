using Hx.Embedding.Timing;

namespace Hx.Embedding.Results;

/// <summary>A single embedding vector + its engine id + dimension + the engine's timing (FR-011).</summary>
public sealed record EmbeddingVector(string Engine, int Dimension, IReadOnlyList<float> Vector, EngineTiming Timing);

/// <summary>One engine's cosine for a single pair, with that engine's timing.</summary>
public sealed record SimilarityResult(string Engine, double Cosine, EngineTiming Timing);

/// <summary>A full pairwise cosine matrix for an item set, from a single engine.</summary>
public sealed record PairMatrix(IReadOnlyList<string> Items, double[][] Cosines);

/// <summary>A near-duplicate group at a threshold (member indexes into the input + their texts).</summary>
public sealed record Cluster(IReadOnlyList<int> MemberIndexes, IReadOnlyList<string> Members)
{
    public int Size => Members.Count;
}

/// <summary>Cross-engine agreement for one pair: each engine's cosine + the absolute divergence.</summary>
public sealed record EngineAgreement(string EngineA, double CosineA, string EngineB, double CosineB, double Divergence);

/// <summary>Per-engine matrix result (the element of the <c>--engine both</c> array for <c>matrix</c>).</summary>
public sealed record EngineMatrix(string Engine, PairMatrix Matrix, EngineTiming Timing);

/// <summary>Per-engine dedupe result (the element of the <c>--engine both</c> array for <c>dedupe</c>).</summary>
public sealed record EngineDedupe(string Engine, double Threshold, IReadOnlyList<Cluster> Clusters, EngineTiming Timing);

/// <summary>One row of <c>compare-engines</c>: the pair + its cross-engine agreement.</summary>
public sealed record PairComparison(int I, int J, string TextI, string TextJ, EngineAgreement Agreement);

/// <summary>The <c>compare-engines</c> payload: both engine ids + timings + every per-pair comparison.</summary>
public sealed record EngineComparison(
    string EngineA, string EngineB, EngineTiming TimingA, EngineTiming TimingB, IReadOnlyList<PairComparison> Pairs);
