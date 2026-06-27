using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Embedding;
using Hx.Semantic;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    /// <summary>
    /// 008 M-5/FR-042: the operator surface for the advisory semantic drift finder — <c>hx doti drift-candidates</c>,
    /// composed into the packed tool over <c>Hx.Semantic.Core</c> (mirroring <c>hx impact</c>). It NEVER gates: when no
    /// model/engine is provisioned the finder raises <see cref="SemanticException"/> and this surfaces a SKIP (exit 0),
    /// never a failure. The result reports the active engine id (FR-042) and an explicit absence note — an empty
    /// candidate list is advisory-incomplete, not a clean bill of health (FR-019).
    /// </summary>
    public static CliResult DotiDriftCandidates(CliMeta meta, string repo, string baseRef, string modelRoot, double threshold)
    {
        string root = Path.GetFullPath(repo);
        string resolvedBase = string.IsNullOrWhiteSpace(baseRef) ? "HEAD" : baseRef;
        // FR-041: an explicit --model-root wins (dev override), else config.LlmModelRoot wins over HEUREX_LLM_ROOT
        // (resolved inside ModelLocator). The config read is a single-field, executable-adjacent lookup kept inline so
        // the runner stays free of a Hx.Scaffold.Core dependency (it composes into hx but never depends on it).
        string? resolvedModelRoot = !string.IsNullOrWhiteSpace(modelRoot)
            ? modelRoot
            : ResolveConfiguredModelRoot();

        try
        {
            DriftCandidatesResult result = DriftCandidateRunner.Run(
                root, resolvedBase, resolvedModelRoot, threshold > 0 ? threshold : null);
            return CliResults.Ok(meta, "doti drift-candidates",
                $"{result.Candidates.Count} advisory candidate(s) via {result.ActiveEngine} ({result.ChunksEmbedded} chunk(s) embedded); absence is NOT a clean-bill signal.",
                result);
        }
        catch (SemanticException ex)
        {
            // No provisioned engine / model, or an unresolved change set — advisory only, so a SKIP, never a gate failure.
            return CliResults.Skipped(meta, "doti drift-candidates", $"semantic finder skipped (advisory): {ex.Message}");
        }
    }

    /// <summary>FR-041: read <c>llmModelRoot</c> from the executable-adjacent <c>hx.config.json</c> (config WINS over the
    /// env var). A minimal single-field read — the full schema/validation lives in Hx.Scaffold.Core, which the runner
    /// must not reference; returns null when absent so <c>ModelLocator</c> falls back to <c>HEUREX_LLM_ROOT</c>.</summary>
    private static string? ResolveConfiguredModelRoot()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "hx.config.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (doc.RootElement.TryGetProperty("llmModelRoot", out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                string root = value.GetString() ?? "";
                return string.IsNullOrWhiteSpace(root) ? null : root;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A malformed/unreadable config is not fatal for an advisory finder — fall through to the env var.
        }

        return null;
    }
}
