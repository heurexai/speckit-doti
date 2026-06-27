# .NET code↔doc calibration gold set (009, FR-013 / SC-007)

The advisory drift finder (`hx doti drift-candidates`) embeds changed C# members and reference prose, then
surfaces cross-category pairs above a per-engine cosine threshold as "go look here" candidates — never a verdict,
never gating. This document records the **.NET-specific recalibration** of those thresholds (009 Work Item 5) and the
evidence behind the committed constants in
[`tools/Hx.Embedding.Core/Thresholds.cs`](../../tools/Hx.Embedding.Core/Thresholds.cs).

## What changed in 009

1. **Member-level chunking** — a `.cs` file is split into one chunk per type/member by the lexer-aware
   `CSharpMemberChunker` (it masks string/char/comment spans before counting braces), so a stale doc lines up against
   the *specific* member that drifted, not the whole file. Non-`.cs` documents keep the existing whole-document
   chunking.
2. **A code/.NET instruction on the Qwen3 drift path** — Qwen3 (the instruction-following decoder) applies the same
   instruction prefix to **both** sides of the symmetric comparison, biasing it toward "does this C# member's
   behaviour still match this prose?". BGE-M3 ignores `EmbedTask` and stays instruction-free, so its embeddings are
   byte-identical with or without the instruction (the symmetry contract, FR-015, holds for both engines).

The instruction is:

> Given a C#/.NET code member and a documentation passage, assess whether they describe the same behaviour, API
> surface, or intent.

## The gold set

[`test/Hx.Semantic.Tests/Fixtures/dotnet-gold-set.json`](../../test/Hx.Semantic.Tests/Fixtures/dotnet-gold-set.json) —
10 labelled .NET code↔doc pairs (6 `related`, 4 `unrelated`). A `related` pair is a doc passage that is *about* a
member — including the drift cases where the prose went stale:

| id | label | the drift it models |
|---|---|---|
| `renamed-method-stale-doc` | related | method renamed `Authenticate`→`SignIn`; XML-doc still says "Authenticate" |
| `changed-signature-stale-readme` | related | method gained a `CancellationToken`; README describes the old 2-arg form |
| `property-semantics-shifted` | related | `Count` (int) → `IsEmpty` (bool); doc still describes a numeric count |
| `matching-code-doc-add` | related | `Add(int,int)` whose doc still matches (a true negative for *drift*, still a related pair) |
| `validation-method-doc` | related | a negative-total guard vs prose describing the same validation rule |
| `retry-policy-doc` | related | an exponential-backoff retry helper vs prose explaining the backoff policy |
| `auth-code-vs-billing-doc` | unrelated | a sign-in method vs invoice-billing prose |
| `math-code-vs-network-doc` | unrelated | `Add` vs TLS-handshake configuration prose |
| `queue-code-vs-logging-doc` | unrelated | `IsEmpty` vs structured-logging-sink prose |
| `validate-code-vs-cache-doc` | unrelated | a validation guard vs cache-eviction prose |

## Measured cosines (models at `D:\LLM-Models`, 2026-06-28)

Qwen3-Embedding-0.6B (GGUF f16) and BGE-M3 (ONNX), run over the gold set. `instructed` = the symmetric code/.NET
instruction applied to both sides; `plain` = no instruction (the general path).

| pair | label | Qwen3 instructed | Qwen3 plain | BGE-M3 (instr == plain) |
|---|---|---|---|---|
| renamed-method-stale-doc | related | 0.8932 | 0.8057 | 0.8389 |
| changed-signature-stale-readme | related | 0.8784 | 0.7703 | 0.7743 |
| property-semantics-shifted | related | 0.6866 | 0.5673 | 0.6757 |
| matching-code-doc-add | related | 0.9157 | 0.8386 | 0.8133 |
| validation-method-doc | related | 0.8216 | 0.7855 | 0.7824 |
| retry-policy-doc | related | 0.7442 | 0.5525 | 0.7425 |
| auth-code-vs-billing-doc | unrelated | 0.2343 | 0.1707 | 0.3926 |
| math-code-vs-network-doc | unrelated | 0.2097 | 0.1951 | 0.3699 |
| queue-code-vs-logging-doc | unrelated | 0.2372 | 0.1350 | 0.3460 |
| validate-code-vs-cache-doc | unrelated | 0.2360 | 0.2231 | 0.3379 |

**Bands:**

- Qwen3 instructed — related `[0.6866, 0.9157]`, unrelated `[0.2097, 0.2372]`.
- BGE-M3 — related `[0.6757, 0.8389]`, unrelated `[0.3379, 0.3926]` (instruction-free; instructed == plain).

## Chosen thresholds and the precision/recall band

| engine | old (general) | new (.NET) | gold-set precision / recall at new |
|---|---|---|---|
| Qwen3 | 0.72 | **0.62** | 1.0 / 1.0 |
| BGE-M3 | 0.81 | **0.55** | 1.0 / 1.0 |

Each new threshold sits in the centre of a band that gives perfect separation on the gold set:

- **Qwen3 0.62** — any threshold in `[0.55, 0.68]` yields precision 1.0 / recall 1.0; 0.62 is the midpoint and leaves a
  ~0.45 margin to the highest unrelated cosine (0.2372). The old general 0.72 dropped to **recall 0.83** here (it
  missed the `property-semantics-shifted` pair at instructed 0.6866).
- **BGE-M3 0.55** — any threshold in `[0.50, 0.65]` yields precision 1.0 / recall 1.0. The old general 0.81 was far too
  tight for code↔doc: it caught only 2 of 6 related pairs (**recall 0.33**).

The finder is recall-favouring and never gating, so the lower thresholds are the correct posture — a few extra "look
here" candidates cost a human glance, a missed stale doc costs a drift.

## SC-007: a .NET drift the general thresholds miss

Two `related` pairs that the **general** path (plain whole-file-style embedding, general Qwen3 threshold 0.72) misses
are surfaced by the **tuned** path (member chunk + symmetric instruction, .NET threshold 0.62):

| pair | general (plain) cos vs 0.72 | tuned (instructed) cos vs 0.62 |
|---|---|---|
| `retry-policy-doc` | 0.5525 → **MISSED** | 0.7442 → **surfaced** |
| `property-semantics-shifted` | 0.5673 → **MISSED** | 0.6866 → **surfaced** |

The instruction is what rescues them: for `retry-policy-doc` it lifts the cosine from 0.5525 to 0.7442.

## Presence gating (arch-review M3)

The calibration tests ([`DotNetCalibrationTests`](../../test/Hx.Semantic.Tests/DotNetCalibrationTests.cs)) **skip**
(via `Assert.Skip`) when the models are absent or unpinned, so the suite stays green in a model-less CI. Model
inference is environment-dependent and not bit-deterministic; the **committed thresholds are the durable artifact**
and the gate never re-runs inference. The model presence gate reuses `ModelLocator.ModelsPresent` (the existing
hash-verified-presence check). The model root requires a pinned `model.version.json` (BL-4); the digests recorded
there are the SHA-256 of the provisioned assets.
