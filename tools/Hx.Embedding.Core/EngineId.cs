namespace Hx.Embedding;

/// <summary>The two engines that run side by side (encoder vs decoder).</summary>
public enum EngineId
{
    BgeM3,
    Qwen3,
}

/// <summary>
/// Stable wire ids — the JSON value every result carries, independent of the enum member names (the envelope's
/// camelCase <c>JsonStringEnumConverter</c> would otherwise emit "bgeM3"/"qwen3"). Pinning the wire form keeps
/// the engine-id a stable contract (FR-016).
/// </summary>
public static class EngineIds
{
    public const string BgeM3 = "bge-m3";
    public const string Qwen3 = "qwen3-0.6b";

    public static string Wire(EngineId id) => id switch
    {
        EngineId.BgeM3 => BgeM3,
        EngineId.Qwen3 => Qwen3,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown engine."),
    };
}
