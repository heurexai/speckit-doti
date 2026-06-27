namespace Hx.Embedding;

/// <summary>
/// Resolves engine model + tokenizer paths, <b>fail-closed</b>: throws <see cref="SemanticException"/> if no root is
/// set, the root is absent, a required file is missing, or its SHA-256 does not match the pinned
/// <c>model.version.json</c> (BL-4 — verified BEFORE load). No machine path is hard-coded — subpaths are relative to
/// the resolved root. FR-041: the root precedence (config wins, then env, then fail-hard) is applied by the CALLER
/// passing the config root as <paramref name="root"/>; this locator's own fallback is <c>root ?? HEUREX_LLM_ROOT ?? throw</c>.
/// </summary>
public sealed class ModelLocator
{
    public const string RootEnvVar = "HEUREX_LLM_ROOT";

    private readonly string _root;
    private readonly ModelManifestValidator _manifest;

    /// <param name="root">The model root (config-resolved by the caller, FR-041); otherwise <c>HEUREX_LLM_ROOT</c>.</param>
    public ModelLocator(string? root = null)
    {
        _root = root
            ?? Environment.GetEnvironmentVariable(RootEnvVar)
            ?? throw new SemanticException(
                $"No model root set (config llmModelRoot, then {RootEnvVar}); fail-closed, never guessed.");

        if (!Directory.Exists(_root))
        {
            throw new SemanticException($"Model root '{_root}' does not exist.");
        }

        _manifest = ModelManifestValidator.Load(_root);
    }

    /// <summary>The BGE-M3 ONNX graph. BL-4 (T037): ONNX Runtime loads the external weight sidecar
    /// <c>model.onnx.data</c> from the same dir at session creation, so a poisoned sidecar would bypass the
    /// <c>model.onnx</c> hash — verify it too when present (fail-closed: a present-but-unpinned sidecar throws).</summary>
    public string BgeM3Model
    {
        get
        {
            string model = RequireVerifiedFile("ONNX", "bge-m3", "model.onnx");
            string sidecar = Path.Combine(_root, "ONNX", "bge-m3", "model.onnx.data");
            if (File.Exists(sidecar))
            {
                _manifest.Verify("ONNX/bge-m3/model.onnx.data", sidecar);
            }

            return model;
        }
    }

    /// <summary>The BlingFire XLM-RoBERTa tokenizer model provisioned next to the BGE-M3 model.</summary>
    public string BgeM3Tokenizer => RequireVerifiedFile("ONNX", "bge-m3", "xlm_roberta_base.bin");

    /// <summary>The Qwen3-Embedding-0.6B GGUF (F16, quality-first).</summary>
    public string Qwen3Model => RequireVerifiedFile("GGUF", "Qwen3-Embedding-0.6B", "Qwen3-Embedding-0.6B-f16.gguf");

    /// <summary><c>true</c> when every v1 engine asset is present AND hash-verified (lets model-backed tests skip honestly).</summary>
    public bool ModelsPresent
    {
        get
        {
            try
            {
                _ = BgeM3Model;
                _ = BgeM3Tokenizer;
                _ = Qwen3Model;
                return true;
            }
            catch (SemanticException)
            {
                return false;
            }
        }
    }

    // BL-4: existence + pinned-SHA-256 verification BEFORE the path is handed to the native loader.
    private string RequireVerifiedFile(params string[] relativeSegments)
    {
        string relativeKey = string.Join('/', relativeSegments);
        string full = Path.Combine(new[] { _root }.Concat(relativeSegments).ToArray());
        if (!File.Exists(full))
        {
            throw new SemanticException(
                $"Required model asset not found: '{full}'. Provision it under the model root (fail-closed).");
        }

        _manifest.Verify(relativeKey, full);
        return full;
    }
}
