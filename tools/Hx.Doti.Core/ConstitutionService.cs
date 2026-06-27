namespace Hx.Doti.Core;

/// <summary>
/// The result of reading a project's constitution (<c>.doti/memory/constitution.md</c>): whether it exists, the full
/// content, and the <b>§2 — Project declarations</b> projection (a VERBATIM substring from the <c>## §2</c> anchor to
/// EOF, so the injected content is byte-identical to on-disk — 009 SC-003). <see cref="AbsenceNote"/> carries an
/// actionable surface-and-proceed message when the file is missing (FR-016) or present without a §2 section.
/// </summary>
public sealed record ConstitutionReadResult(
    bool Exists,
    string? FullContent,
    string? Section2Content,
    string? AbsenceNote,
    string Path);

/// <summary>
/// Reads the project constitution as the carrier for the codified fresh-context injection (009 FR-006/007/008) and the
/// on-demand <c>hx doti constitution</c> tool. Single responsibility: locate <c>.doti/memory/constitution.md</c>, read
/// it, and project §2 — never throws on absence (surface-and-proceed, FR-016). Lives in <c>Hx.Doti.Core</c> because the
/// constitution is a Doti asset; the runner composes it (it is never reached from <c>Hx.Cycle.Core</c>).
/// </summary>
public static class ConstitutionService
{
    /// <summary>Repo-relative path to the active constitution (what plan + arch-review read).</summary>
    public const string RelativePath = ".doti/memory/constitution.md";

    /// <summary>The stable §2 anchor the template and every authored constitution carry (009 M1, arch-review).
    /// Matched as a line that, trimmed, starts with this — so the §2 slice is deterministic and operator-edit-robust.</summary>
    public const string Section2Anchor = "## §2";

    public static ConstitutionReadResult Read(string repositoryRoot)
    {
        string path = System.IO.Path.Combine(repositoryRoot, ".doti", "memory", "constitution.md");
        string display = RelativePath;
        if (!File.Exists(path))
        {
            return new ConstitutionReadResult(
                Exists: false,
                FullContent: null,
                Section2Content: null,
                AbsenceNote: $"No constitution at {RelativePath} — run /doti-constitution to author one. "
                    + "Optional advisory context; the §1 invariants stay gate-enforced regardless.",
                Path: display);
        }

        string content = File.ReadAllText(path);
        string? section2 = ExtractSection2(content);
        return new ConstitutionReadResult(
            Exists: true,
            FullContent: content,
            Section2Content: section2,
            AbsenceNote: section2 is null
                ? $"Constitution present at {RelativePath} but no '{Section2Anchor}' section found; emitting the full file."
                : null,
            Path: display);
    }

    /// <summary>The VERBATIM substring from the first <c>## §2</c> heading line to EOF (byte-identical to on-disk,
    /// SC-003), or <c>null</c> when no §2 anchor is present. Locates the line-start offset in the raw string and slices
    /// from there — no re-rendering — so it is robust to CRLF/LF and preserves operator whitespace exactly.</summary>
    public static string? ExtractSection2(string content)
    {
        int lineStart = 0;
        while (lineStart <= content.Length)
        {
            int newline = content.IndexOf('\n', lineStart);
            int rawEnd = newline < 0 ? content.Length : newline;
            int lineEnd = rawEnd > lineStart && content[rawEnd - 1] == '\r' ? rawEnd - 1 : rawEnd;
            ReadOnlySpan<char> line = content.AsSpan(lineStart, lineEnd - lineStart);
            if (line.TrimStart().StartsWith(Section2Anchor.AsSpan(), StringComparison.Ordinal))
            {
                return content[lineStart..];
            }

            if (newline < 0)
            {
                break;
            }

            lineStart = newline + 1;
        }

        return null;
    }
}
