# Hx semantic calibration record

The per-engine cosine thresholds the semantic stack uses (`Hx.Embedding.Core/Thresholds.cs`) are **committed
constants**, calibrated on labelled gold sets and recorded here. The gate never re-runs inference — the number is the
durable artifact. Model-backed calibration tests are presence-gated (they `Assert.Skip` when the models are absent),
so a model-less CI stays green.

## .NET code↔doc drift (009, FR-013 / SC-007)

The advisory drift finder (`hx doti drift-candidates`) compares changed C# members against reference prose and
surfaces close cross-category pairs as "go look here" candidates — recall-favouring, advisory, never gating, never
wired into a gate or proof.

**009 changes:** member-level chunking of `.cs` (the lexer-aware `CSharpMemberChunker`) plus a symmetric code/.NET
instruction on the **Qwen3** drift path (BGE-M3 stays instruction-free). Recalibrated per engine on the labelled .NET
gold set (`test/Hx.Semantic.Tests/Fixtures/dotnet-gold-set.json`, 6 related / 4 unrelated pairs):

| engine | old (general) | new (.NET) | gold-set precision / recall |
|---|---|---|---|
| Qwen3 | 0.72 | **0.62** | 1.0 / 1.0 |
| BGE-M3 | 0.81 | **0.55** | 1.0 / 1.0 |

- Qwen3 instructed bands: related `[0.6866, 0.9157]`, unrelated `[0.2097, 0.2372]` — 0.62 is the midpoint of a
  perfect-separation band `[0.55, 0.68]`.
- BGE-M3 bands: related `[0.6757, 0.8389]`, unrelated `[0.3379, 0.3926]` — 0.55 is the midpoint of `[0.50, 0.65]`.
  The old general 0.81 caught only 2 of 6 related pairs (recall 0.33), far too tight for code↔doc.

The instruction rescues real .NET drift the general path misses: `retry-policy-doc` (plain 0.5525 → instructed 0.7442)
and `property-semantics-shifted` (plain 0.5673 → instructed 0.6866) cross the tuned 0.62 threshold but not the general
0.72.

Full method, the per-pair cosine table, and the SC-007 evidence: [`docs/calibration/009-dotnet-gold-set.md`](../calibration/009-dotnet-gold-set.md).
