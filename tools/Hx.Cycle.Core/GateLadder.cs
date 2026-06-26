using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>How the declared tier runs a gate step (FR-029). <c>Enforced</c> = run, fail closed on failure;
/// <c>Advisory</c> = run, report but never fail the gate; <c>Skip</c> = do not run (Skipped).</summary>
public enum GateMode
{
    Enforced,
    Advisory,
    Skip,
}

/// <summary>
/// The gate ladder owned by a repo's declared <b>tier</b> (FR-029) — distinct from the <c>--profile</c>
/// <b>lane</b> (auto/advisory/normal/release), which is the orthogonal "how hard" axis. The tier is read from
/// <c>.doti/integration.json</c> (<c>profile</c>) → <c>.doti/profiles/&lt;name&gt;/profile.json</c> (<c>gates</c>),
/// and declares the mode for each opinionated step. A step the tier does not declare defaults to
/// <see cref="GateMode.Enforced"/> — today's behavior — so the model is opt-out, never a silent weakening.
/// </summary>
public sealed record GateLadder(string Tier, IReadOnlyDictionary<string, GateMode> Steps)
{
    public GateMode ModeFor(string step) =>
        Steps.TryGetValue(step, out GateMode mode) ? mode : GateMode.Enforced;

    /// <summary>The canonical, ordered coverage entries recorded in the <see cref="GateProof"/> (FR-029): one
    /// per declared step, sorted, so a narrowed/downgraded ladder is detectable by recompute.</summary>
    public IReadOnlyList<GateLadderEntry> Coverage() =>
        Steps.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new GateLadderEntry(kv.Key, kv.Value.ToString().ToLowerInvariant()))
            .ToList();
}

/// <summary>The outcome of resolving a repo's tier ladder. <see cref="Ladder"/> is null when resolution fails
/// closed (FR-030): a declared profile whose <c>profile.json</c> is missing or malformed.</summary>
public sealed record GateLadderResolution(GateLadder? Ladder, string? FailureReason)
{
    public bool Ok => Ladder is not null;
}

/// <summary>
/// Resolves a repo's <see cref="GateLadder"/> from its declared tier. A repo with no
/// <c>.doti/integration.json</c> defaults to the non-imposing <c>workflow-only</c> tier (FR-030: doti installs
/// into existing code without dropping Heurex gates onto it). A declared profile whose <c>profile.json</c> is
/// absent or unparseable fails closed (FR-030; the <c>gate run</c> step maps this to
/// <c>Integrity_ProfileGateMissingConfig</c>).
/// </summary>
public static class GateLadderResolver
{
    public const string WorkflowOnlyTier = "workflow-only";

    // dotnet-cli is the legacy self-host profile name; it is the dotnet-cli-heurex (Tier 3) ladder. T030
    // migrates the recorded name; until then the two resolve to the same Tier-3 gates.
    private static readonly IReadOnlyDictionary<string, string> TierAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["dotnet-cli"] = "dotnet-cli-heurex" };

    public static GateLadderResolution Resolve(string repositoryRoot)
    {
        string integrationPath = Path.Combine(repositoryRoot, ".doti", "integration.json");
        if (!File.Exists(integrationPath))
        {
            // Non-imposing default: only the cycle/chokepoint baseline; opinionated gates skip.
            return new GateLadderResolution(new GateLadder(WorkflowOnlyTier, WorkflowOnlyGates()), null);
        }

        string? declaredProfile = ReadProfileName(integrationPath);
        if (string.IsNullOrWhiteSpace(declaredProfile))
        {
            return new GateLadderResolution(null, ".doti/integration.json is missing or has no 'profile'");
        }

        string tier = TierAliases.TryGetValue(declaredProfile, out string? aliased) ? aliased : declaredProfile;
        string profilePath = Path.Combine(repositoryRoot, ".doti", "profiles", declaredProfile, "profile.json");
        if (!File.Exists(profilePath))
        {
            return new GateLadderResolution(null,
                $"declared tier '{declaredProfile}' has no profile at .doti/profiles/{declaredProfile}/profile.json");
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(profilePath));
            var steps = new Dictionary<string, GateMode>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("gates", out JsonElement gates) && gates.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty gate in gates.EnumerateObject())
                {
                    if (TryParseMode(gate.Value.GetString(), out GateMode mode))
                    {
                        steps[gate.Name] = mode;
                    }
                    else
                    {
                        return new GateLadderResolution(null,
                            $"tier '{tier}' declares gate '{gate.Name}' with an invalid mode '{gate.Value.GetString()}'");
                    }
                }
            }

            return new GateLadderResolution(new GateLadder(tier, steps), null);
        }
        catch (JsonException ex)
        {
            return new GateLadderResolution(null, $"declared tier '{declaredProfile}' has a malformed profile.json: {ex.Message}");
        }
    }

    private static string? ReadProfileName(string integrationPath)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(integrationPath));
            return doc.RootElement.TryGetProperty("profile", out JsonElement profile) ? profile.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryParseMode(string? value, out GateMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "enforced": mode = GateMode.Enforced; return true;
            case "advisory": mode = GateMode.Advisory; return true;
            case "skip": mode = GateMode.Skip; return true;
            default: mode = GateMode.Enforced; return false;
        }
    }

    // Tier 1: the cycle + chokepoint baseline only; the opinionated Heurex structural gates are skipped so doti
    // does not impose Sentrux/ArchUnitNET on a repo that did not opt in.
    private static IReadOnlyDictionary<string, GateMode> WorkflowOnlyGates() =>
        new Dictionary<string, GateMode>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentrux-verify"] = GateMode.Skip,
            ["sentrux-check"] = GateMode.Skip,
            ["architecture-test"] = GateMode.Skip,
        };
}
