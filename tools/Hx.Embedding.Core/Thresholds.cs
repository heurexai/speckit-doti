namespace Hx.Embedding;

/// <summary>
/// Per-engine dedupe thresholds. The two engines operate at different cosine scales, so the near-duplicate
/// threshold is calibrated <b>per engine</b> on the labelled gold set (SC-002) and recorded in
/// <c>docs/plans/hx-semantic-calibration.md</c>. These are the calibrated defaults the CLI uses when no explicit
/// <c>--threshold</c> is given.
/// </summary>
public static class Thresholds
{
    /// <summary>
    /// The calibrated default "semantically related" threshold for an engine (cosine, inclusive) — NOT a
    /// duplicate authority. Calibrated (T029) on a 30-pair labelled set drawn from human-annotated benchmarks
    /// (PAWS-Wiki label=1/0 + STS-B): the robust midpoint of the gap between UNRELATED pairs and SEMANTICALLY
    /// RELATED pairs. It catches every true near-duplicate and rejects every unrelated pair, and deliberately
    /// ALSO flags lexically-similar opposites ("river A tributary of B" vs "river B tributary of A") — cosine
    /// cannot tell a duplicate from a compositional opposite (PAWS dup/hard bands overlap at 0.89–0.99), so
    /// dedupe is a recall-favouring CANDIDATE generator whose precise dup-vs-opposite call is a Stage-2 NLI
    /// cross-encoder (proven; see docs/plans/hx-semantic-calibration.md). The threshold is DOMAIN-SENSITIVE
    /// (these Wikipedia sentences calibrate ~0.81; short persona statements ~0.67) — recalibrate per domain, or
    /// pass --threshold.
    /// </summary>
    public static double Default(EngineId id) => id switch
    {
        EngineId.BgeM3 => 0.81,
        EngineId.Qwen3 => 0.72,
        _ => 0.81,
    };
}
