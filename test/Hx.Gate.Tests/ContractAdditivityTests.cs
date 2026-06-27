using System.Text.Json;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Gate.Tests;

/// <summary>
/// 012 (T001, M2 — schema additivity): every 012 contract addition is nullable/defaulted so a pre-012 persisted
/// gate-proof / <c>--json</c> envelope still deserializes and <c>SchemaVersion</c> need not bump. A required field
/// would break reading older proofs at release-train validation.
/// </summary>
public sealed class ContractAdditivityTests
{
    private static readonly JsonSerializerOptions Options = JsonContractSerializerOptions.Create();

    [Fact]
    public void A_pre_012_gate_run_result_json_without_trace_still_deserializes()
    {
        // A pre-012 envelope: a GateRunResult with NO `trace` and a GateStep with NO `durationMs`.
        const string preFeatureJson = """
        {
          "schemaVersion": 1,
          "lane": { "lane": "Normal", "outcome": "Pass", "reason": "normal" },
          "proof": {
            "schemaVersion": 1,
            "outcome": "Pass",
            "steps": [ { "name": "hygiene", "outcome": "Pass", "evidence": [ { "kind": "h", "message": "ok" } ] } ],
            "evidence": []
          }
        }
        """;

        GateRunResult? result = JsonSerializer.Deserialize<GateRunResult>(preFeatureJson, Options);

        Assert.NotNull(result);
        Assert.Null(result!.Trace); // additive nullable — absent in the old proof
        GateStep step = Assert.Single(result.Proof.Steps);
        Assert.Null(step.DurationMs); // additive nullable — absent in the old proof
        Assert.Equal(StageOutcome.Pass, result.Proof.Outcome);
    }

    [Fact]
    public void GateStep_round_trips_with_and_without_duration()
    {
        var withDuration = new GateStep("hygiene", StageOutcome.Pass, [new GateEvidence("h", "ok")], DurationMs: 33);
        var withoutDuration = new GateStep("hygiene", StageOutcome.Pass, [new GateEvidence("h", "ok")]);

        GateStep a = JsonSerializer.Deserialize<GateStep>(JsonSerializer.Serialize(withDuration, Options), Options)!;
        GateStep b = JsonSerializer.Deserialize<GateStep>(JsonSerializer.Serialize(withoutDuration, Options), Options)!;

        Assert.Equal(33, a.DurationMs);
        Assert.Null(b.DurationMs);
    }

    [Fact]
    public void GateRunResult_serializes_the_trace_on_the_envelope_when_present()
    {
        var trace = new GateTrace(
            new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []),
            new ChangeSummary(1, 0, 0, 0, 1, 0, ["src/a.cs"], [], false),
            null,
            [],
            5,
            GateEffectiveMode.Partial);
        var proof = new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Pass, [], []);
        var result = new GateRunResult(JsonContractDefaults.SchemaVersion,
            new LaneDecision(Lane.Normal, StageOutcome.Pass, "normal"), proof, trace);

        string json = JsonSerializer.Serialize(result, Options);
        using JsonDocument doc = JsonDocument.Parse(json);

        // The trace is on the ENVELOPE, not nested under the hashed proof (M1).
        Assert.True(doc.RootElement.TryGetProperty("trace", out JsonElement traceElement));
        Assert.Equal("partial", traceElement.GetProperty("effectiveMode").GetString());
        Assert.False(doc.RootElement.GetProperty("proof").TryGetProperty("trace", out _));
    }
}
