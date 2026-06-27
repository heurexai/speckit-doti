namespace Hx.Embedding.Timing;

/// <summary>
/// Per-engine timing carried on every result (FR-011): the one-time model-load cost separated from
/// per-embedding throughput, so experiments compare quality vs speed across engines.
/// </summary>
public sealed record EngineTiming(string Engine, long ModelLoadMs, int EmbedCount, long TotalEmbedMs)
{
    /// <summary>Mean per-embedding latency (ms); 0 before any embedding.</summary>
    public double MeanEmbedMs => EmbedCount > 0 ? (double)TotalEmbedMs / EmbedCount : 0;
}
