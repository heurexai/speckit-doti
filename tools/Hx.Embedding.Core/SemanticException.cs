namespace Hx.Embedding;

/// <summary>
/// A fail-closed semantic-core error: a missing/invalid model or tokenizer asset, an unset
/// <c>HEUREX_LLM_ROOT</c>, or invalid input. The CLI maps it to a coded <c>VAL0001</c> validation envelope so
/// the failure is a clear, machine-readable error — never a silent degrade or an opaque crash (FR-008/FR-014).
/// </summary>
public sealed class SemanticException : Exception
{
    public SemanticException(string message) : base(message)
    {
    }

    public SemanticException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
