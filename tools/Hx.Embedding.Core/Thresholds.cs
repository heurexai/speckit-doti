namespace Hx.Embedding;

/// <summary>
/// Per-engine drift thresholds for the advisory code↔doc finder. The two engines operate at different cosine scales,
/// so the "semantically related" threshold is calibrated <b>per engine</b> on a labelled gold set and recorded in
/// <c>docs/plans/hx-semantic-calibration.md</c> + <c>docs/calibration/009-dotnet-gold-set.md</c>. These are committed
/// constants (FR-013/SC-007): the gate never re-runs inference, so the durable artifact is the number, not a
/// re-computation. They are the defaults <see cref="Hx.Semantic"/>'s <c>DriftCandidateService</c> uses when no
/// explicit <c>--threshold</c> is given.
/// </summary>
public static class Thresholds
{
    /// <summary>
    /// The calibrated default "this code member and this doc passage are about the same thing — go look" threshold for
    /// an engine (cosine, inclusive). The finder is recall-favouring and NEVER gating, so the band favours catching a
    /// stale-doc pair over precision.
    /// <para>
    /// <b>.NET-calibrated (009, FR-013/SC-007)</b> on the labelled .NET code↔doc gold set
    /// (<c>test/Hx.Semantic.Tests/Fixtures/dotnet-gold-set.json</c>; 6 related / 4 unrelated pairs), with member-level
    /// chunking and — for Qwen3 — the symmetric code/.NET instruction. Both numbers sit in the centre of a band that
    /// cleanly separates the related and unrelated cosines (precision 1.0 / recall 1.0 on the gold set):
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Qwen3 0.62</b> (was 0.72): instructed related cosines 0.69–0.92, unrelated 0.21–0.24. The 0.72 form
    ///   missed real .NET drift (a retry-policy method vs its prose at plain-cosine 0.55; a count→IsEmpty property at
    ///   0.57); the instructed path lifts them to 0.74 / 0.69 and 0.62 surfaces them.</item>
    ///   <item><b>BGE-M3 0.55</b> (was 0.81): related cosines 0.68–0.84, unrelated 0.34–0.39. The 0.81 form caught only
    ///   2 of 6 related pairs (recall 0.33) — far too tight for code↔doc; 0.55 catches all 6.</item>
    /// </list>
    /// </summary>
    public static double Default(EngineId id) => id switch
    {
        EngineId.BgeM3 => 0.55,
        EngineId.Qwen3 => 0.62,
        _ => 0.55,
    };
}
