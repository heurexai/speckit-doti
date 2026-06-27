using System.Diagnostics;
using Hx.Embedding.Timing;
using LLama;
using LLama.Common;
using LLama.Native;

namespace Hx.Embedding.Engines;

/// <summary>
/// Qwen3-Embedding-0.6B via LLamaSharp over a CPU GGUF (Engine B). Last-token pooling (declared in the GGUF and
/// forced via <see cref="LLamaPoolingType.Last"/>); EOS is auto-appended by the model's tokenizer. The
/// <see cref="EmbedRole.Symmetric"/> role embeds raw text with no prefix (the persona use case); only
/// <see cref="EmbedRole.Query"/> applies the instruction prefix. Result L2-normalized. Owns native resources.
/// </summary>
internal sealed class Qwen3Embedder : IEmbedder
{
    public const int Dim = 1024;

    private readonly LLamaWeights _weights;
    private readonly LLamaEmbedder _embedder;
    private readonly EngineOptions _options;
    private readonly long _modelLoadMs;
    private int _embedCount;
    private long _totalEmbedMs;
    private bool _disposed;

    public Qwen3Embedder(string modelPath, EngineOptions options)
    {
        _options = options;
        var stopwatch = Stopwatch.StartNew();

        var parameters = new ModelParams(modelPath)
        {
            Embeddings = true,
            PoolingType = LLamaPoolingType.Last,
            GpuLayerCount = 0,
            Threads = options.Threads,
            ContextSize = (uint)options.ContextSize,
            BatchSize = (uint)options.BatchSize,
            UBatchSize = (uint)options.UBatchSize, // pooled embeddings need the whole sequence in one ubatch
        };

        try
        {
            _weights = LLamaWeights.LoadFromFile(parameters);
        }
        catch (Exception ex)
        {
            throw new SemanticException($"Failed to load Qwen3 GGUF '{modelPath}': {ex.Message}", ex);
        }

        try
        {
            _embedder = new LLamaEmbedder(_weights, parameters);
        }
        catch (Exception ex)
        {
            _weights.Dispose(); // release the already-acquired native weights — no leak on partial load
            throw new SemanticException($"Failed to create Qwen3 embedder for '{modelPath}': {ex.Message}", ex);
        }

        _modelLoadMs = stopwatch.ElapsedMilliseconds;
    }

    public string Id => EngineIds.Qwen3;

    public int Dimension => Dim;

    public EngineTiming Timing => new(Id, _modelLoadMs, _embedCount, _totalEmbedMs);

    public float[] Embed(string text, EmbedTask task)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new SemanticException("Cannot embed empty or whitespace text.");
        }

        string prompt = task.Role == EmbedRole.Query && !string.IsNullOrEmpty(task.Instruction)
            ? $"Instruct: {task.Instruction}\nQuery:{text}"
            : text; // Symmetric / Document = raw text (no asymmetric prefix)
        prompt = FitToTokenBudget(prompt);

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<float[]> embeddings = _embedder.GetEmbeddings(prompt).GetAwaiter().GetResult();
        if (embeddings.Count == 0)
        {
            throw new SemanticException("Qwen3 returned no embedding for the input.");
        }

        float[] normalized = Vectors.Normalize(embeddings[0]);

        _embedCount++;
        _totalEmbedMs += stopwatch.ElapsedMilliseconds;
        return normalized;
    }

    // Deterministic truncate-to-fit: keep the largest character prefix whose token count ≤ budget (with headroom
    // for the auto-appended EOS), so the pooled sequence always fits one ubatch (FR-012/FR-014).
    private string FitToTokenBudget(string text)
    {
        int budget = Math.Max(1, _options.MaxTokens - 8);
        if (TokenCount(text) <= budget)
        {
            return text;
        }

        int lo = 0;
        int hi = text.Length;
        int best = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (TokenCount(text[..mid]) <= budget)
            {
                best = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // Don't cut through a UTF-16 surrogate pair (a lone high surrogate would be invalid input).
        if (best > 0 && char.IsHighSurrogate(text[best - 1]))
        {
            best--;
        }

        return text[..best];
    }

    // Count tokens via the MODEL handle (alive for the embedder's lifetime) — NOT the embedder's context,
    // which GetEmbeddings disposes after each call.
    private int TokenCount(string text) =>
        _weights.NativeHandle.Tokenize(text, false, false, System.Text.Encoding.UTF8).Length;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _embedder.Dispose();
        _weights.Dispose();
    }
}
