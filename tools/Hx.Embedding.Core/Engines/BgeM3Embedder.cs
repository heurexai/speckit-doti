using System.Diagnostics;
using Hx.Embedding.Timing;
using Microsoft.ML.OnnxRuntime;

namespace Hx.Embedding.Engines;

/// <summary>
/// BGE-M3 dense embedder over a CPU ONNX session (Engine A). Flow (verified by parity): XLM-R tokenize → wrap
/// <c>&lt;s&gt;…&lt;/s&gt;</c> → forward pass with <c>input_ids</c>+<c>attention_mask</c> → take the
/// <c>dense_vecs</c> output (CLS-pooled by the export) → L2-normalize. Symmetric — ignores
/// <see cref="EmbedTask"/>. Owns the session + tokenizer; dispose it.
/// </summary>
internal sealed class BgeM3Embedder : IEmbedder
{
    public const int Dim = 1024;

    private readonly XlmRobertaTokenizer _tokenizer;
    private readonly InferenceSession _session;
    private readonly string _outputName;
    private readonly EngineOptions _options;
    private readonly long _modelLoadMs;
    private int _embedCount;
    private long _totalEmbedMs;
    private bool _disposed;

    public BgeM3Embedder(string modelPath, string tokenizerPath, EngineOptions options)
    {
        _options = options;
        var stopwatch = Stopwatch.StartNew();

        _tokenizer = new XlmRobertaTokenizer(tokenizerPath);
        try
        {
            var sessionOptions = new SessionOptions { IntraOpNumThreads = options.Threads, InterOpNumThreads = 1 };
            _session = new InferenceSession(modelPath, sessionOptions); // by path → external model.onnx.data resolves
        }
        catch (Exception ex)
        {
            _tokenizer.Dispose(); // release the already-acquired native tokenizer handle — no leak on partial load
            throw new SemanticException($"Failed to load BGE-M3 ONNX model '{modelPath}': {ex.Message}", ex);
        }

        // Resolve the dense output BY NAME (the aapot export emits dense_vecs + sparse_vecs + colbert_vecs);
        // fail-closed if a differently-shaped export lacks it, rather than silently using output[0].
        const string DenseVecs = "dense_vecs";
        if (!_session.OutputMetadata.ContainsKey(DenseVecs))
        {
            _session.Dispose();
            _tokenizer.Dispose();
            throw new SemanticException(
                $"BGE-M3 ONNX model '{modelPath}' has no '{DenseVecs}' output (found: {string.Join(", ", _session.OutputMetadata.Keys)}).");
        }

        _outputName = DenseVecs;
        _modelLoadMs = stopwatch.ElapsedMilliseconds;
    }

    public string Id => EngineIds.BgeM3;

    public int Dimension => Dim;

    public EngineTiming Timing => new(Id, _modelLoadMs, _embedCount, _totalEmbedMs);

    public float[] Embed(string text, EmbedTask task)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new SemanticException("Cannot embed empty or whitespace text.");
        }

        var stopwatch = Stopwatch.StartNew();

        long[] ids = _tokenizer.Encode(text, _options.MaxTokens); // BGE-M3 is symmetric: task is ignored
        long length = ids.Length;
        var mask = new long[length];
        Array.Fill(mask, 1L);
        var shape = new long[] { 1, length };

        using var inputIds = OrtValue.CreateTensorValueFromMemory(ids, shape);
        using var attentionMask = OrtValue.CreateTensorValueFromMemory(mask, shape);
        var inputs = new Dictionary<string, OrtValue>(StringComparer.Ordinal)
        {
            ["input_ids"] = inputIds,
            ["attention_mask"] = attentionMask,
        };

        using var runOptions = new RunOptions();
        using var outputs = _session.Run(runOptions, inputs, new[] { _outputName });
        ReadOnlySpan<float> data = outputs[0].GetTensorDataAsSpan<float>();

        // dense_vecs is [1,1024] (CLS-pooled + dense by the export); the first 1024 floats are the CLS vector
        // whether the export emits [1,1024] or a [1,L,1024] hidden state (CLS = first token).
        var vector = new float[Dim];
        for (int i = 0; i < Dim; i++)
        {
            vector[i] = data[i];
        }

        float[] normalized = Vectors.Normalize(vector);

        _embedCount++;
        _totalEmbedMs += stopwatch.ElapsedMilliseconds;
        return normalized;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Dispose();
        _tokenizer.Dispose();
    }
}
