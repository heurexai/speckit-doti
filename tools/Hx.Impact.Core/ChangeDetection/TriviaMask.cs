namespace Hx.Impact.Core.ChangeDetection;

/// <summary>
/// A minimal hand-rolled C# lexer that produces a character-level trivia mask: <c>true</c> where a character is part
/// of a comment or the inside of a string/char literal (delimiters masked too), so the caller can scan real
/// structural tokens (braces, declaration keywords) without a <c>{</c>/<c>class</c> hidden in a string or comment
/// moving structure. Ported from the <c>Hx.Embedding.CSharpMemberChunker</c> masking discipline (arch-review L1) so
/// <see cref="TopLevelTypeScanner"/> keeps Impact's zero-Roslyn, no-cross-core-dep boundary. Defensive on
/// unterminated literals (masks to a safe stop).
/// </summary>
internal static class TriviaMask
{
    public static bool[] Build(string s)
    {
        var masked = new bool[s.Length];
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '/' && Next(s, i) == '/') { i = MaskLineComment(s, masked, i); continue; }
            if (c == '/' && Next(s, i) == '*') { i = MaskBlockComment(s, masked, i); continue; }
            if (StartsRawString(s, i)) { i = MaskRawString(s, masked, i); continue; }
            if (StartsVerbatimString(s, i)) { i = MaskVerbatimString(s, masked, i); continue; }
            if (c == '"' || (c == '$' && Next(s, i) == '"')) { i = MaskRegularString(s, masked, i); continue; }
            if (c == '\'') { i = MaskCharLiteral(s, masked, i); continue; }
            i++;
        }

        return masked;
    }

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

    private static int MaskLineComment(string s, bool[] masked, int i)
    {
        while (i < s.Length && s[i] != '\n')
        {
            masked[i++] = true;
        }

        return i;
    }

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

        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                int run = 0;
                while (i + run < s.Length && s[i + run] == '"')
                {
                    run++;
                }

                for (int k = 0; k < run; k++)
                {
                    masked[i + k] = true;
                }
                i += run;
                if (run >= quoteRun)
                {
                    return i;
                }

                continue;
            }

            masked[i++] = true;
        }

        return i;
    }

    private static int MaskVerbatimString(string s, bool[] masked, int i)
    {
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
                if (i + 1 < s.Length && s[i + 1] == '"')
                {
                    masked[i++] = true;
                    masked[i++] = true;
                    continue;
                }

                masked[i++] = true;
                return i;
            }

            masked[i++] = true;
        }

        return i;
    }

    private static int MaskRegularString(string s, bool[] masked, int i)
    {
        if (s[i] == '$')
        {
            masked[i++] = true;
        }

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

            if (c == '"')
            {
                masked[i++] = true;
                return i;
            }

            if (c == '\n')
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
                return i;
            }

            masked[i++] = true;
        }

        return i;
    }
}
