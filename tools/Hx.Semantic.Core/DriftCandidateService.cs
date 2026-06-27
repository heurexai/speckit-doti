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
    /// <summary>
    /// The code/.NET-oriented instruction Qwen3 (only) applies symmetrically to both sides of the drift comparison
    /// (FR-013). It biases the decoder toward "does this C# member's behaviour still match this prose?" — sharpening
    /// the code↔docs signal. BGE-M3 ignores <see cref="EmbedTask"/> entirely, so it stays instruction-free.
    /// </summary>
    internal const string CodeDriftInstruction =
        "Given a C#/.NET code member and a documentation passage, assess whether they describe the same behaviour, API surface, or intent.";

    public DriftCandidatesResult Run(
        string repositoryRoot, ChangeSetContext changeSet, EngineSelection engine, double? threshold = null)
    {
        (IReadOnlyList<DriftChunk> code, IReadOnlyList<DriftChunk> reference) = BuildChunks(repositoryRoot, changeSet);
        double effectiveThreshold = threshold ?? Thresholds.Default(engine.Active);

        // Qwen3 carries the code instruction symmetrically; BGE-M3 ignores it (stays instruction-free) — FR-013.
        EmbedTask task = EmbedTask.SymmetricInstructed(CodeDriftInstruction);
        IReadOnlyList<SemanticCandidate> candidates =
            new DriftCandidateFinder(engine.Embedder).Find(code, reference, effectiveThreshold, task: task);

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
                // FR-013: split a .cs file into one chunk per type/member (lexer-aware) so a stale doc lines up against
                // the specific member that changed, not the whole file. A file with no splittable member falls back to
                // a single whole-file chunk (CSharpMemberChunker returns one chunk in that case).
                foreach (SourceChunk member in CSharpMemberChunker.Chunk(file.Path, text))
                {
                    code.Add(new DriftChunk(file.Path, "runtime-code", member.Text));
                }
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
