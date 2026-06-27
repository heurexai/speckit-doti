namespace Hx.Impact.Core.ChangeDetection;

/// <summary>
/// 012 (arch-review L1): a <b>lexer-aware</b> top-level C# type-name scanner for the change-set summary's
/// "classes touched" measure. It masks string/char literals, <c>//</c> and <c>/* */</c> comments, and verbatim/
/// interpolated/raw strings BEFORE scanning, so a <c>class</c>/<c>record</c> token hidden in a string or comment is
/// never counted — the same masking discipline as <c>Hx.Embedding.CSharpMemberChunker</c> (zero Roslyn). It records
/// the name after each <c>class|struct|record|interface|enum</c> keyword that appears at the top type level (brace
/// depth 0 for a file-scoped namespace, or 1 inside a single block namespace), so members and nested types deeper in
/// a body are not counted. Recall-favouring and deterministic — telemetry, never gating.
/// </summary>
public static class TopLevelTypeScanner
{
    private static readonly string[] TypeKeywords = ["class", "struct", "record", "interface", "enum"];

    /// <summary>Top-level type names declared in <paramref name="source"/>, in first-seen Ordinal order, deduped.</summary>
    public static IReadOnlyList<string> Scan(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length == 0)
        {
            return [];
        }

        bool[] masked = TriviaMask.Build(source);
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int braceDepth = 0;
        // The brace depth at which we entered the current outermost type body, or -1 when not inside any type. Set on
        // the '{' that opens a recorded type's body; cleared on its matching '}'. This distinguishes namespace braces
        // (no type body) from type braces, so file-scoped (types at depth 0) and block namespaces (types at depth 1)
        // both yield top-level types only — members/nested types inside a type body are skipped.
        int insideTypeDepth = -1;
        bool pendingTypeBrace = false;

        int i = 0;
        while (i < source.Length)
        {
            if (masked[i])
            {
                i++;
                continue;
            }

            char c = source[i];
            if (c == '{')
            {
                braceDepth++;
                if (pendingTypeBrace)
                {
                    insideTypeDepth = braceDepth;
                    pendingTypeBrace = false;
                }

                i++;
                continue;
            }

            if (c == '}')
            {
                if (insideTypeDepth == braceDepth)
                {
                    insideTypeDepth = -1; // left the outermost type body
                }

                braceDepth = braceDepth > 0 ? braceDepth - 1 : 0;
                i++;
                continue;
            }

            // A declaration keyword is a TOP-LEVEL type only when we are not currently inside any type body. The next
            // '{' will open this type's body (marked by pendingTypeBrace). A body-less declaration (e.g. a record with
            // a primary ctor and a ';') never opens a body, so pendingTypeBrace is reset at the terminating ';'.
            if (insideTypeDepth == -1 && IsWordStart(source, i) && MatchTypeKeyword(source, masked, i) is { } afterKeyword)
            {
                int next = SkipWhitespaceAndMask(source, masked, afterKeyword);
                string? name = ReadIdentifier(source, masked, next);
                if (name is not null && seen.Add(name))
                {
                    names.Add(name);
                    pendingTypeBrace = true;
                }

                i = next;
                continue;
            }

            // A ';' before the type's '{' means a body-less declaration (e.g. `public record Gadget(int X);`) — it
            // owns no body, so cancel the pending type-brace so the following declaration is still seen as top-level.
            if (c == ';' && pendingTypeBrace)
            {
                pendingTypeBrace = false;
            }

            i++;
        }

        return names;
    }

    // Returns the index just past a matched type keyword (whole word, unmasked), or null. `record class`/`record
    // struct` resolve to the same keyword family — the first keyword wins and its following identifier is the name.
    private static int? MatchTypeKeyword(string s, bool[] masked, int i)
    {
        foreach (string keyword in TypeKeywords)
        {
            int end = i + keyword.Length;
            if (end > s.Length || !s.AsSpan(i, keyword.Length).SequenceEqual(keyword))
            {
                continue;
            }

            if (AnyMasked(masked, i, end) || (end < s.Length && IsIdentifierChar(s[end])))
            {
                continue; // a longer identifier that merely starts with the keyword (e.g. "classroom")
            }

            return end;
        }

        return null;
    }

    private static string? ReadIdentifier(string s, bool[] masked, int start)
    {
        if (start >= s.Length || masked[start] || !IsIdentifierStart(s[start]))
        {
            return null;
        }

        int end = start;
        while (end < s.Length && !masked[end] && IsIdentifierChar(s[end]))
        {
            end++;
        }

        // `record class Foo` / `record struct Foo`: the captured token is the secondary keyword, not a name — re-read.
        string token = s[start..end];
        if (Array.IndexOf(TypeKeywords, token) >= 0)
        {
            int next = SkipWhitespaceAndMask(s, masked, end);
            return ReadIdentifier(s, masked, next);
        }

        return token;
    }

    private static int SkipWhitespaceAndMask(string s, bool[] masked, int i)
    {
        while (i < s.Length && (masked[i] || char.IsWhiteSpace(s[i])))
        {
            i++;
        }

        return i;
    }

    private static bool IsWordStart(string s, int i) => i == 0 || !IsIdentifierChar(s[i - 1]);

    private static bool AnyMasked(bool[] masked, int start, int end)
    {
        for (int k = start; k < end; k++)
        {
            if (masked[k])
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_' || c == '@';

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
