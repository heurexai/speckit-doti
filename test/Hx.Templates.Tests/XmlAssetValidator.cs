using System.Xml;

namespace Hx.Templates.Tests;

/// <summary>
/// Well-formedness linter for the XML assets a scaffold template ships. The template's MSBuild/solution XML
/// (.csproj/.props/.slnx, …) is INERT in this repo — it is copied verbatim into a generated repo and is never
/// loaded by THIS repo's own build — so an XML defect (an illegal <c>--</c> inside a comment, an unescaped
/// <c>&amp;</c>, a mismatched tag, a bad entity) ships undetected until a generated repo is built
/// (<c>MSB4024</c> → no <c>TargetFramework</c> → <c>NETSDK1013</c>).
///
/// This parses each asset with the BCL <see cref="XmlReader"/> — the canonical .NET XML linter and the very
/// parser MSBuild loads project files through, so "well-formed here" == "loadable there", with zero third-party
/// or native dependency. Scope is the RAW (pre-<c>dotnet new</c>) template; validity AFTER symbol substitution is
/// the round-trip build's job (<see cref="TemplateRoundTripTests"/>).
/// </summary>
internal static class XmlAssetValidator
{
    // The .NET / MSBuild / solution / packaging XML asset extensions — domain-complete, so a newly added XML
    // asset type cannot silently bypass validation. (The first cut hard-coded only .csproj/.props/.slnx — the
    // three types that happened to exist — which would have let a future .targets/.nuspec/.config slip through.)
    private static readonly HashSet<string> XmlAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".fsproj", ".vbproj", ".props", ".targets",        // MSBuild
        ".sln", ".slnx", ".slnf",                                     // solutions
        ".nuspec", ".config", ".ruleset", ".runsettings", ".resx",    // packaging / config / test / resources
        ".manifest", ".xaml", ".vsixmanifest", ".xml",                // app / tooling / generic
    };

    /// <summary>Parse every discovered XML asset; return one <see cref="XmlAssetDefect"/> per not-well-formed file.</summary>
    internal static IReadOnlyList<XmlAssetDefect> Validate(string root)
    {
        var defects = new List<XmlAssetDefect>();
        foreach (string path in DiscoverXmlAssets(root))
        {
            try
            {
                // XmlReader honors the document's encoding declaration and reports the exact line/column on a
                // well-formedness violation. DTD processing is off — no external fetch, no entity expansion.
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using XmlReader reader = XmlReader.Create(path, settings);
                while (reader.Read())
                {
                }
            }
            catch (XmlException ex)
            {
                defects.Add(new XmlAssetDefect(path, ex.LineNumber, ex.LinePosition, ex.Message));
            }
        }

        return defects;
    }

    /// <summary>
    /// Every file under <paramref name="root"/> that is a .NET XML asset by extension, OR declares itself XML with
    /// a leading <c>&lt;?xml</c> declaration (an explicit-intent catch for an unlisted extension), ordinal-sorted.
    /// </summary>
    internal static IReadOnlyList<string> DiscoverXmlAssets(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => XmlAssetExtensions.Contains(Path.GetExtension(p)) || DeclaresXml(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    private static bool DeclaresXml(string path)
    {
        // An XML declaration must be the very first thing in the file (no leading whitespace is permitted before
        // it), so a non-XML file with leading markup — a "<div>" in markdown, an "<svg>" asset — never matches:
        // this only rescues a genuinely-declared XML document that carries an extension the set above omits.
        using var reader = new StreamReader(path);
        char[] head = new char[5];
        int read = reader.Read(head, 0, head.Length);
        return read == 5 && new string(head).Equals("<?xml", StringComparison.Ordinal);
    }
}

/// <summary>A not-well-formed XML asset: the offending file with the parser's exact line/column and message.</summary>
internal readonly record struct XmlAssetDefect(string Path, int Line, int Column, string Message)
{
    public override string ToString() => $"{Path}({Line},{Column}): {Message}";
}
