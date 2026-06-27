namespace Hx.Embedding;

/// <summary>
/// Vector math used across the core: L2 normalization (zero-norm safe — never produces NaN) and cosine
/// similarity (clamped to [-1,1]). The zero-norm/clamp guards keep a degenerate input (e.g. an empty line that
/// embeds to a zero vector) from emitting a non-finite score the JSON envelope cannot serialize (FR-014).
/// </summary>
public static class Vectors
{
    private const double Epsilon = 1e-12;

    /// <summary>Return an L2-normalized copy of <paramref name="v"/>. A zero/degenerate vector (norm &lt; 1e-12)
    /// returns all-zeros — defined and finite, never NaN.</summary>
    public static float[] Normalize(float[] v)
    {
        double sum = 0;
        for (int i = 0; i < v.Length; i++)
        {
            sum += (double)v[i] * v[i];
        }

        var result = new float[v.Length];
        double norm = Math.Sqrt(sum);
        if (norm < Epsilon)
        {
            return result; // zero vector — no direction
        }

        float inverse = (float)(1.0 / norm);
        for (int i = 0; i < v.Length; i++)
        {
            result[i] = v[i] * inverse;
        }

        return result;
    }

    /// <summary>Cosine similarity of two equal-length vectors, robust to (un)normalization and clamped to
    /// [-1,1]. A zero vector (no direction) yields 0 rather than NaN (FR-014/SC-001).</summary>
    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new SemanticException($"Vector length mismatch: {a.Length} vs {b.Length}.");
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        if (normA < Epsilon * Epsilon || normB < Epsilon * Epsilon)
        {
            return 0; // a zero vector has no direction
        }

        double cosine = dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        return Math.Clamp(cosine, -1.0, 1.0);
    }
}
