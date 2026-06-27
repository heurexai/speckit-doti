using System.Text;

namespace Hx.Embedding.Engines;

/// <summary>
/// XLM-RoBERTa tokenizer for BGE-M3 via BlingFire (MIT). BlingFire emits HF-compatible XLM-R ids (XLM-R base/
/// large share the 250002 vocab) but does NOT add special tokens — so we wrap <c>&lt;s&gt;(0) … &lt;/s&gt;(2)</c>
/// ourselves and truncate to a token budget. Owns the native model handle; dispose it.
/// </summary>
internal sealed class XlmRobertaTokenizer : IDisposable
{
    private const int Bos = 0;
    private const int Eos = 2;
    private const int Unk = 3;

    private readonly ulong _model;
    private bool _disposed;

    public XlmRobertaTokenizer(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new SemanticException($"Tokenizer model not found: '{modelPath}'.");
        }

        _model = BlingFireNative.LoadModel(Encoding.UTF8.GetBytes(modelPath));
        if (_model == 0)
        {
            throw new SemanticException($"Failed to load XLM-RoBERTa tokenizer model: '{modelPath}'.");
        }
    }

    /// <summary>Tokenize to model ids wrapped with BOS/EOS, truncated so the total length ≤ <paramref name="maxTokens"/>.</summary>
    public long[] Encode(string text, int maxTokens)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var buffer = new int[Math.Max(8, bytes.Length + 2)]; // token count ≤ byte count for BPE
        int n = BlingFireNative.TextToIds(_model, bytes, bytes.Length, buffer, buffer.Length, Unk);
        n = Math.Clamp(n, 0, buffer.Length);

        int body = Math.Min(n, Math.Max(0, maxTokens - 2)); // reserve 2 for BOS/EOS
        var ids = new long[body + 2];
        ids[0] = Bos;
        for (int i = 0; i < body; i++)
        {
            ids[i + 1] = buffer[i];
        }

        ids[body + 1] = Eos;
        return ids;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        BlingFireNative.FreeModel(_model);
    }
}
