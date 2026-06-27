using System.Text.Json;
using Hx.Embedding;
using Xunit;

namespace Hx.Semantic.Tests;

/// <summary>
/// FR-013 / SC-007 (arch-review M3): the .NET code↔doc calibration gold set. The committed per-engine thresholds in
/// <see cref="Thresholds"/> are the durable artifact — these tests re-derive them from the labelled gold set
/// (<c>Fixtures/dotnet-gold-set.json</c>) ONLY when the models are present, and <b>SKIP</b> (never fail) when they are
/// absent, so the suite stays green in a model-less CI. Model inference is environment-dependent and not bit-
/// deterministic; the gate never re-runs it.
/// <para>
/// The "drift / no-drift" framing: a <c>related</c> pair (a doc passage ABOUT a member — incl. a renamed method or a
/// changed signature whose prose went stale) is exactly where doc-drift hides and the finder SHOULD surface a
/// candidate; an <c>unrelated</c> pair (a member vs a doc about a different API) should NOT be surfaced. The committed
/// threshold is a recall-favouring midpoint that separates the two bands.
/// </para>
/// </summary>
public sealed class DotNetCalibrationTests
{
    /// <summary>The code/.NET instruction Qwen3 (only) applies symmetrically on the drift path — mirrors
    /// <c>DriftCandidateService.CodeDriftInstruction</c>.</summary>
    private const string CodeDriftInstruction =
        "Given a C#/.NET code member and a documentation passage, assess whether they describe the same behaviour, API surface, or intent.";

    private sealed record Pair(string Id, string Label, string Code, string Doc);

    private static IReadOnlyList<Pair> LoadGoldSet()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "dotnet-gold-set.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var pairs = new List<Pair>();
        foreach (JsonElement e in doc.RootElement.GetProperty("pairs").EnumerateArray())
        {
            pairs.Add(new Pair(
                e.GetProperty("id").GetString()!,
                e.GetProperty("label").GetString()!,
                e.GetProperty("code").GetString()!,
                e.GetProperty("doc").GetString()!));
        }
        return pairs;
    }

    /// <summary>Presence gate (M3): a ModelLocator whose ModelsPresent is false ⇒ the test SKIPS, not fails.</summary>
    private static ModelLocator? PresentLocatorOrSkip()
    {
        ModelLocator locator;
        try
        {
            locator = new ModelLocator();
        }
        catch (SemanticException)
        {
            Assert.Skip("Embedding models not provisioned (no model root) — calibration runs only in the model-present lane.");
            return null;
        }

        if (!locator.ModelsPresent)
        {
            Assert.Skip("Embedding models absent/unpinned — calibration runs only in the model-present lane (committed thresholds are the durable artifact).");
            return null;
        }

        return locator;
    }

    private static double Cosine(IEmbedder embedder, SimilarityService sim, string code, string doc, EmbedTask task) =>
        sim.Cosine(embedder.Embed(code, task), embedder.Embed(doc, task));

    [Fact]
    public void Gold_set_is_well_formed()
    {
        IReadOnlyList<Pair> pairs = LoadGoldSet();
        Assert.Contains(pairs, p => p.Label == "related");
        Assert.Contains(pairs, p => p.Label == "unrelated");
        Assert.All(pairs, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Code));
            Assert.False(string.IsNullOrWhiteSpace(p.Doc));
            Assert.Contains(p.Label, new[] { "related", "unrelated" });
        });
    }

    [Fact]
    public void Qwen3_committed_threshold_separates_related_from_unrelated_on_the_dotnet_gold_set()
    {
        ModelLocator? locator = PresentLocatorOrSkip();
        if (locator is null) return;

        IReadOnlyList<Pair> pairs = LoadGoldSet();
        var sim = new SimilarityService();
        double threshold = Thresholds.Default(EngineId.Qwen3);
        EmbedTask task = EmbedTask.SymmetricInstructed(CodeDriftInstruction); // the drift path: Qwen3 carries the instruction

        using IEmbedder embedder = new SemanticEngineFactory(locator).Create(EngineId.Qwen3);

        var related = new List<double>();
        var unrelated = new List<double>();
        foreach (Pair p in pairs)
        {
            double cos = Cosine(embedder, sim, p.Code, p.Doc, task);
            (p.Label == "related" ? related : unrelated).Add(cos);
        }

        // The committed threshold catches EVERY related pair (recall 1.0) and rejects EVERY unrelated pair
        // (precision 1.0) on the gold set.
        Assert.All(related, cos => Assert.True(cos >= threshold, $"a related pair scored {cos:F4} < threshold {threshold}"));
        Assert.All(unrelated, cos => Assert.True(cos < threshold, $"an unrelated pair scored {cos:F4} >= threshold {threshold}"));
        Assert.True(related.Min() - unrelated.Max() > 0.05, "related and unrelated bands must be cleanly separated");
    }

    [Fact]
    public void BgeM3_committed_threshold_separates_related_from_unrelated_and_stays_instruction_free()
    {
        ModelLocator? locator = PresentLocatorOrSkip();
        if (locator is null) return;

        IReadOnlyList<Pair> pairs = LoadGoldSet();
        var sim = new SimilarityService();
        double threshold = Thresholds.Default(EngineId.BgeM3);

        using IEmbedder embedder = new SemanticEngineFactory(locator).Create(EngineId.BgeM3);

        var related = new List<double>();
        var unrelated = new List<double>();
        foreach (Pair p in pairs)
        {
            double cos = Cosine(embedder, sim, p.Code, p.Doc, EmbedTask.Symmetric);
            (p.Label == "related" ? related : unrelated).Add(cos);

            // BGE-M3 ignores EmbedTask — the instructed and plain embeddings are byte-identical (FR-013 contract:
            // BGE-M3 stays instruction-free).
            double instructed = Cosine(embedder, sim, p.Code, p.Doc, EmbedTask.SymmetricInstructed(CodeDriftInstruction));
            Assert.Equal(cos, instructed, 6);
        }

        Assert.All(related, cos => Assert.True(cos >= threshold, $"a related pair scored {cos:F4} < threshold {threshold}"));
        Assert.All(unrelated, cos => Assert.True(cos < threshold, $"an unrelated pair scored {cos:F4} >= threshold {threshold}"));
        Assert.True(related.Min() - unrelated.Max() > 0.05, "related and unrelated bands must be cleanly separated");
    }

    [Fact]
    public void Tuned_qwen3_path_surfaces_a_dotnet_drift_the_general_threshold_misses()
    {
        ModelLocator? locator = PresentLocatorOrSkip();
        if (locator is null) return;

        IReadOnlyList<Pair> pairs = LoadGoldSet();
        var sim = new SimilarityService();
        const double GeneralThreshold = 0.72; // the pre-009 general Qwen3 threshold
        double tuned = Thresholds.Default(EngineId.Qwen3);
        EmbedTask instructed = EmbedTask.SymmetricInstructed(CodeDriftInstruction);

        using IEmbedder embedder = new SemanticEngineFactory(locator).Create(EngineId.Qwen3);

        // SC-007: at least one related .NET pair that the GENERAL path (plain embedding, general 0.72 threshold) would
        // miss is SURFACED by the tuned path (instructed embedding, .NET-calibrated threshold).
        int rescued = 0;
        foreach (Pair p in pairs.Where(p => p.Label == "related"))
        {
            double plain = Cosine(embedder, sim, p.Code, p.Doc, EmbedTask.Symmetric);
            double instr = Cosine(embedder, sim, p.Code, p.Doc, instructed);

            bool generalMisses = plain < GeneralThreshold;
            bool tunedSurfaces = instr >= tuned;
            if (generalMisses && tunedSurfaces)
            {
                rescued++;
            }
        }

        Assert.True(rescued >= 1,
            "the .NET-tuned finder must surface >= 1 .NET-specific code↔doc drift the general thresholds miss");
    }
}
