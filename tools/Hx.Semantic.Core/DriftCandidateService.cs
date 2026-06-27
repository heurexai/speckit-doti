using Hx.Embedding;
using Hx.Tooling.Contracts;

namespace Hx.Semantic;

/// <summary>
/// FR-018 orchestration: build the code/reference chunks from a <see cref="ChangeSetContext"/> (no own <c>git diff</c>
/// — it consumes the already-collected change set), run the <see cref="DriftCandidateFinder"/> with the selected
/// engine, and report the ACTIVE engine (FR-042). Advisory only — the result carries no proof.
/// </summary>
public sealed class DriftCandidateService
{
    public DriftCandidatesResult Run(
        string repositoryRoot, ChangeSetContext changeSet, EngineSelection engine, double? threshold = null)
    {
        (IReadOnlyList<DriftChunk> code, IReadOnlyList<DriftChunk> reference) = BuildChunks(repositoryRoot, changeSet);
        double effectiveThreshold = threshold ?? Thresholds.Default(engine.Active);
        IReadOnlyList<SemanticCandidate> candidates =
            new DriftCandidateFinder(engine.Embedder).Find(code, reference, effectiveThreshold);

        return new DriftCandidatesResult(
            JsonContractDefaults.SchemaVersion,
            engine.ActiveWireId,
            code.Count + reference.Count,
            candidates,
            "ADVISORY: an empty candidate list is NOT a clean-bill signal — only the deterministic /08-doti-drift-review gate clears drift.");
    }

    private static (IReadOnlyList<DriftChunk> Code, IReadOnlyList<DriftChunk> Reference) BuildChunks(
        string repositoryRoot, ChangeSetContext changeSet)
    {
        var code = new List<DriftChunk>();
        var reference = new List<DriftChunk>();
        foreach (ChangedFile file in changeSet.Files)
        {
            if (file.Status == ChangeStatus.Deleted)
            {
                continue; // no content to read for a deleted file
            }

            string? text = TryRead(repositoryRoot, file.Path);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (IsCode(file.Path))
            {
                code.Add(new DriftChunk(file.Path, "runtime-code", text));
            }
            else if (IsProse(file.Path))
            {
                reference.Add(new DriftChunk(file.Path, "prose", text));
            }
        }

        // Anchor the comparison against the authoritative prose even when it did not change this cycle.
        foreach (string path in new[] { ".doti/agent-context.md", "README.md" })
        {
            string? text = TryRead(repositoryRoot, path);
            if (!string.IsNullOrWhiteSpace(text) && reference.All(r => !string.Equals(r.Path, path, StringComparison.Ordinal)))
            {
                reference.Add(new DriftChunk(path, "prose", text));
            }
        }

        return (code, reference);
    }

    private static bool IsCode(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsProse(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".doti/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryRead(string repositoryRoot, string relativePath)
    {
        try
        {
            string full = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) ? File.ReadAllText(full) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
