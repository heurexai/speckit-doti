using Hx.Embedding.Timing;

namespace Hx.Embedding;

/// <summary>How a text participates in a comparison — drives engine-specific prompting.</summary>
public enum EmbedRole
{
    /// <summary>Statement-vs-statement comparison (similarity/matrix/dedupe). No asymmetric instruction —
    /// both sides are embedded identically so <c>cosine(x,y) == cosine(y,x)</c> (FR-015).</summary>
    Symmetric,

    /// <summary>A retrieval query — Qwen3 applies its instruction prefix. Reserved for explicit retrieval /
    /// reproducing an engine's published reference (parity).</summary>
    Query,

    /// <summary>A retrieval document — raw text, no prefix.</summary>
    Document,

    /// <summary>A SYMMETRIC comparison where Qwen3 (the instruction-following decoder) applies the same instruction
    /// prefix to BOTH sides — so <c>cosine(x,y) == cosine(y,x)</c> still holds (FR-015) — while BGE-M3 (which ignores
    /// <see cref="EmbedTask"/>) stays instruction-free. Used by the advisory drift finder to bias Qwen3 toward
    /// code↔docs semantics without breaking symmetry. FR-013.</summary>
    SymmetricInstructed,
}

/// <summary>The per-text task: a role plus the optional engine-specific instruction used only by <see cref="EmbedRole.Query"/>.</summary>
public readonly record struct EmbedTask(EmbedRole Role, string? Instruction = null)
{
    /// <summary>The default for every symmetric comparison command.</summary>
    public static readonly EmbedTask Symmetric = new(EmbedRole.Symmetric);

    /// <summary>A retrieval document (raw text).</summary>
    public static readonly EmbedTask Document = new(EmbedRole.Document);

    /// <summary>A retrieval query carrying the engine instruction.</summary>
    public static EmbedTask Query(string instruction) => new(EmbedRole.Query, instruction);

    /// <summary>A symmetric comparison carrying an instruction the instruction-following engine (Qwen3) applies to both
    /// sides; instruction-free engines (BGE-M3) ignore it. Keeps <c>cosine(x,y) == cosine(y,x)</c>. FR-013.</summary>
    public static EmbedTask SymmetricInstructed(string instruction) => new(EmbedRole.SymmetricInstructed, instruction);
}

/// <summary>
/// A pluggable CPU embedding engine. Implementations load a pinned local model and map text to an
/// L2-normalized vector, so cosine similarity reduces to a dot product. <b>Instances are single-threaded-use</b>
/// — they hold a native session/context that is not safe for concurrent <see cref="Embed"/> calls; a consumer
/// (e.g. the future daemon) pools one per thread (FR-009). Owns native resources — dispose it.
/// </summary>
public interface IEmbedder : IDisposable
{
    /// <summary>The stable wire id of the engine (e.g. "bge-m3").</summary>
    string Id { get; }

    /// <summary>The embedding dimension (1024 for both v1 engines).</summary>
    int Dimension { get; }

    /// <summary>Cumulative timing (one-time model load + per-embedding) for this instance (FR-011).</summary>
    EngineTiming Timing { get; }

    /// <summary>Embed <paramref name="text"/> for <paramref name="task"/>, returning an L2-normalized vector.</summary>
    float[] Embed(string text, EmbedTask task);
}
