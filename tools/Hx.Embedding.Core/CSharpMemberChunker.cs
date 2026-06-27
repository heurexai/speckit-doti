using System.Text;

namespace Hx.Embedding;

/// <summary>
/// A single chunk of a chunked document: a stable <see cref="Label"/> (a path-qualified member hint, used only for
/// diagnostics/snippets) and the verbatim <see cref="Text"/> slice. The text is a contiguous substring of the source —
/// the chunker never re-renders, so a chunk round-trips back into the file unchanged.
/// </summary>
public readonly record struct SourceChunk(string Label, string Text);

/// <summary>
/// FR-013 (arch-review M2): a <b>lexer-aware</b> .NET source chunker for the advisory drift finder. It splits a
/// <c>.cs</c> source string into one chunk per top-level declaration and per type member (class/struct/record/
/// interface/enum, methods, properties, constructors, fields, events), attaching the leading attribute lists and
/// XML-doc / line comments to the member they precede. A non-<c>.cs</c> document is NOT this chunker's concern — the
/// caller falls back to its existing whole-document chunking.
/// <para>
/// The scanner is a minimal hand-rolled C# lexer that masks string literals, char literals, <c>//</c> and
/// <c>/* */</c> comments, verbatim (<c>@"…"</c>), interpolated (<c>$"…"</c> / <c>$@"…"</c>) and raw (<c>"""…"""</c>)
/// strings BEFORE counting braces, so a <c>{</c>/<c>}</c> inside a string or comment never moves the brace depth.
/// Naive brace-counting (which mis-splits on braces in strings/comments) is deliberately avoided.
/// </para>
/// <para>
/// Deterministic, allocation-light, zero new package deps (it keeps <c>Hx.Embedding.Core</c>'s zero-<c>Hx.*</c>
/// boundary). Recall-favouring: it tolerates the odd merged/over-split chunk (the finder is advisory, never gating),
/// but it must not gross-mis-chunk on braces hidden inside strings/comments.
/// </para>
/// </summary>
public static class CSharpMemberChunker
{
    /// <summary>Chunks fall back to the whole document below this many real (non-trivia) characters.</summary>
    private const int MinMeaningfulChars = 1;

    /// <summary>
    /// Split <paramref name="source"/> into member chunks. <paramref name="path"/> seeds each chunk's diagnostic label.
    /// When the source has no splittable declaration (e.g. a file of only usings/comments), a single whole-document
    /// chunk is returned so a caller never loses content.
    /// </summary>
    public static IReadOnlyList<SourceChunk> Chunk(string path, string source)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(source);

        if (source.Length == 0)
        {
            return [];
        }

        // 1. Mask trivia so brace/semicolon counting only sees real structural tokens.
        bool[] masked = BuildTriviaMask(source);

        // 2. Walk the masked source, cutting a chunk at every top-level-and-member boundary.
        var spans = SplitSpans(source, masked);
        if (spans.Count == 0)
        {
            return [new SourceChunk(LabelFor(path, source), source)];
        }

        var chunks = new List<SourceChunk>(spans.Count);
        foreach ((int start, int end) in spans)
        {
            string text = source[start..end];
            if (!HasMeaningfulContent(text, masked, start))
            {
                continue; // a span of only whitespace/usings/trivia is not its own chunk
            }

            chunks.Add(new SourceChunk(LabelFor(path, text), text));
        }

        return chunks.Count == 0 ? [new SourceChunk(LabelFor(path, source), source)] : chunks;
    }

    /// <summary>
    /// A character-level mask: <c>true</c> where the character is part of trivia (a comment or the inside of a string/
    /// char literal) and so must NOT count toward braces/semicolons. Delimiters themselves are masked too — only real
    /// code outside any literal/comment is left unmasked.
    /// </summary>
    private static bool[] BuildTriviaMask(string s)
    {
        var masked = new bool[s.Length];
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '/' && Next(s, i) == '/') { i = MaskLineComment(s, masked, i); continue; }     // //…
            if (c == '/' && Next(s, i) == '*') { i = MaskBlockComment(s, masked, i); continue; }     // /*…*/
            if (StartsRawString(s, i)) { i = MaskRawString(s, masked, i); continue; }                // """…"""
            if (StartsVerbatimString(s, i)) { i = MaskVerbatimString(s, masked, i); continue; }       // @"…" / $@"…"
            if (c == '"' || (c == '$' && Next(s, i) == '"')) { i = MaskRegularString(s, masked, i); continue; } // "…" / $"…"
            if (c == '\'') { i = MaskCharLiteral(s, masked, i); continue; }                          // '…'
            i++; // real, structural character — left unmasked
        }

        return masked;
    }

    /// <summary>The next char, or '\0' past end — keeps the dispatch predicates branch-light.</summary>
    private static char Next(string s, int i) => i + 1 < s.Length ? s[i + 1] : '\0';

    private static bool StartsRawString(string s, int i) =>
        s[i] == '"' && i + 2 < s.Length && s[i + 1] == '"' && s[i + 2] == '"';

    private static bool StartsVerbatimString(string s, int i)
    {
        char c = s[i];
        return (c == '@' && Next(s, i) == '"')
            || (c == '$' && i + 2 < s.Length && s[i + 1] == '@' && s[i + 2] == '"')
            || (c == '@' && i + 2 < s.Length && s[i + 1] == '$' && s[i + 2] == '"');
    }

    /// <summary>Line comment: <c>//</c> … to end of line.</summary>
    private static int MaskLineComment(string s, bool[] masked, int i)
    {
        while (i < s.Length && s[i] != '\n')
        {
            masked[i++] = true;
        }

        return i;
    }

    /// <summary>Block comment: <c>/* … */</c> (not nested in C#).</summary>
    private static int MaskBlockComment(string s, bool[] masked, int i)
    {
        masked[i++] = true; // '/'
        masked[i++] = true; // '*'
        while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/'))
        {
            masked[i++] = true;
        }

        if (i < s.Length)
        {
            masked[i++] = true; // '*'
            if (i < s.Length)
            {
                masked[i++] = true; // '/'
            }
        }

        return i;
    }

    private static int MaskRawString(string s, bool[] masked, int i)
    {
        int quoteRun = 0;
        while (i + quoteRun < s.Length && s[i + quoteRun] == '"')
        {
            quoteRun++;
        }

        for (int k = 0; k < quoteRun; k++)
        {
            masked[i + k] = true;
        }
        i += quoteRun;

        // Close on a run of >= quoteRun quotes.
        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                int run = 0;
                while (i + run < s.Length && s[i + run] == '"')
                {
                    run++;
                }
                int take = run;
                for (int k = 0; k < take; k++)
                {
                    masked[i + k] = true;
                }
                i += take;
                if (take >= quoteRun)
                {
                    return i; // closed
                }
                continue;
            }

            masked[i++] = true;
        }

        return i; // unterminated — masked to EOF (defensive)
    }

    private static int MaskVerbatimString(string s, bool[] masked, int i)
    {
        // Consume the @ / $@ / @$ prefix and the opening quote.
        while (i < s.Length && s[i] != '"')
        {
            masked[i++] = true;
        }
        if (i < s.Length)
        {
            masked[i++] = true; // opening quote
        }

        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                // A doubled "" is an escaped quote, not the terminator.
                if (i + 1 < s.Length && s[i + 1] == '"')
                {
                    masked[i++] = true;
                    masked[i++] = true;
                    continue;
                }

                masked[i++] = true; // closing quote
                return i;
            }

            masked[i++] = true;
        }

        return i;
    }

    private static int MaskRegularString(string s, bool[] masked, int i)
    {
        // Consume an optional $ prefix and the opening quote.
        if (s[i] == '$')
        {
            masked[i++] = true;
        }
        masked[i++] = true; // opening quote

        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\\') // escape: skip the next char
            {
                masked[i++] = true;
                if (i < s.Length)
                {
                    masked[i++] = true;
                }
                continue;
            }

            if (c == '"')
            {
                masked[i++] = true; // closing quote
                return i;
            }

            if (c == '\n') // an unterminated single-line string — stop at the newline (defensive)
            {
                return i;
            }

            masked[i++] = true;
        }

        return i;
    }

    private static int MaskCharLiteral(string s, bool[] masked, int i)
    {
        masked[i++] = true; // opening quote
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\\')
            {
                masked[i++] = true;
                if (i < s.Length)
                {
                    masked[i++] = true;
                }
                continue;
            }

            if (c == '\'')
            {
                masked[i++] = true;
                return i;
            }

            if (c == '\n')
            {
                return i; // defensive: unterminated
            }

            masked[i++] = true;
        }

        return i;
    }

    /// <summary>
    /// Walk the masked source and produce contiguous, non-overlapping spans — one for each type-declaration header and
    /// one per member of a type body. Boundaries are cut: after the <c>{</c> that OPENS a type body (depth 0→1, so the
    /// type's attributes/doc/signature form a header chunk distinct from its first member); after the <c>}</c> that
    /// closes a member body (depth back to 1); after the <c>}</c> that closes a top-level type (depth back to 0); and
    /// after a <c>;</c> at depth 1 (a body-less member — field, abstract/expression-bodied member). A depth-0 <c>;</c>
    /// (top-level <c>using</c>/file-scoped <c>namespace</c>/delegate) is NOT a boundary — that header trivia flows into
    /// the next declaration. Leading attributes/doc-comments/whitespace flow into the member they precede because a
    /// chunk starts immediately after the previous boundary.
    /// </summary>
    private static List<(int Start, int End)> SplitSpans(string s, bool[] masked)
    {
        var spans = new List<(int, int)>();
        int depth = 0;
        int chunkStart = 0;
        bool sawType = false; // becomes true once we enter a type body (depth 0→1)

        for (int i = 0; i < s.Length; i++)
        {
            if (masked[i])
            {
                continue; // trivia never moves structure
            }

            char c = s[i];
            switch (c)
            {
                case '{':
                    depth++;
                    if (depth == 1)
                    {
                        // Opening a top-level type body — cut so the type header (attributes/doc/signature + '{') is
                        // its own chunk and the first member starts fresh.
                        sawType = true;
                        int end = i + 1;
                        spans.Add((chunkStart, end));
                        chunkStart = end;
                    }
                    break;

                case '}':
                    depth--;
                    if (depth < 0)
                    {
                        depth = 0; // defensive against an unbalanced source
                    }

                    // Closing a member body back to type level (1) or a top-level type back to file level (0): cut to
                    // include this closing brace.
                    if (depth <= 1)
                    {
                        int end = i + 1;
                        spans.Add((chunkStart, end));
                        chunkStart = end;
                    }
                    break;

                case ';':
                    // A body-less member ends with ';' at type level (depth 1: a field, auto/abstract/expression-bodied
                    // member). A depth-0 ';' (top-level using/namespace/delegate) is header trivia, NOT a boundary.
                    // Deeper ';' are statements inside a body.
                    if (depth == 1)
                    {
                        int end = i + 1;
                        spans.Add((chunkStart, end));
                        chunkStart = end;
                    }
                    break;
            }
        }

        // Trailing remainder (e.g. a final declaration with no terminator, or stray whitespace).
        if (chunkStart < s.Length)
        {
            spans.Add((chunkStart, s.Length));
        }

        // No type body was ever opened — nothing structural to split (a file of only usings/comments/top-level
        // statements). Return empty so Chunk() falls back to a single whole-document chunk.
        if (!sawType)
        {
            return [];
        }

        return spans;
    }

    /// <summary>True when the span carries any non-trivia, non-whitespace character (a real declaration token).</summary>
    private static bool HasMeaningfulContent(string text, bool[] masked, int spanStart)
    {
        int meaningful = 0;
        for (int k = 0; k < text.Length; k++)
        {
            if (masked[spanStart + k])
            {
                continue;
            }

            char c = text[k];
            if (!char.IsWhiteSpace(c))
            {
                meaningful++;
                if (meaningful >= MinMeaningfulChars)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// A short, stable diagnostic label: the file name plus the first declaration-ish line of the chunk (the first
    /// non-blank, non-attribute, non-doc-comment line), trimmed. Purely cosmetic — never parsed.
    /// </summary>
    private static string LabelFor(string path, string text)
    {
        string file = path.Replace('\\', '/');
        int slash = file.LastIndexOf('/');
        if (slash >= 0)
        {
            file = file[(slash + 1)..];
        }

        string? decl = FirstDeclarationLine(text);
        return decl is null ? file : $"{file}:{decl}";
    }

    private static string? FirstDeclarationLine(string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0
                || line.StartsWith("//", StringComparison.Ordinal)
                || line.StartsWith("/*", StringComparison.Ordinal)
                || line.StartsWith('*')
                || line.StartsWith('[')) // skip attributes/doc/comment lines
            {
                continue;
            }

            return line.Length <= 80 ? line : line[..80];
        }

        return null;
    }
}
