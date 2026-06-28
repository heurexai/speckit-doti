using System;
using System.Linq;
using Xunit;

namespace Hx.Templates.Tests;

/// <summary>
/// Proves the linter actually CATCHES the defect class it guards against — a clean template passing is necessary
/// but NOT sufficient. The validator must fail on malformed XML and be comprehensive about what it inspects
/// (beyond the original three extensions) without false-positives on non-XML files.
/// </summary>
public sealed class XmlAssetValidatorTests
{
    [Fact]
    public void Flags_the_illegal_double_hyphen_comment_that_shipped_in_020()
    {
        string dir = NewTempDir();
        try
        {
            // The exact 020 defect: "--" inside an XML comment ("--company") — innocuous-looking, fatal to parse.
            File.WriteAllText(
                Path.Combine(dir, "Directory.Build.props"),
                "<Project>\n  <!-- holder flows from the --company value -->\n  <PropertyGroup />\n</Project>\n");
            File.WriteAllText(Path.Combine(dir, "Good.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");

            IReadOnlyList<XmlAssetDefect> defects = XmlAssetValidator.Validate(dir);

            XmlAssetDefect defect = Assert.Single(defects);              // only the malformed file is flagged
            Assert.EndsWith("Directory.Build.props", defect.Path);
            Assert.True(defect.Line > 0, "the defect carries the parser's line for the offender");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Discovers_xml_by_dotnet_extension_and_by_declaration_but_not_non_xml()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.targets"), "<Project />\n");                       // by extension (not the original 3)
            File.WriteAllText(Path.Combine(dir, "weird.cfg"), "<?xml version=\"1.0\"?>\n<root />\n");  // by XML declaration
            File.WriteAllText(Path.Combine(dir, "notes.md"), "<div>not xml</div>\n");                 // leading '<' but NOT a declaration
            File.WriteAllText(Path.Combine(dir, "data.json"), "{ \"x\": 1 }\n");

            var discovered = XmlAssetValidator.DiscoverXmlAssets(dir).Select(Path.GetFileName).ToHashSet();

            Assert.Contains("a.targets", discovered);
            Assert.Contains("weird.cfg", discovered);
            Assert.DoesNotContain("notes.md", discovered);   // no false-positive on leading markup
            Assert.DoesNotContain("data.json", discovered);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Well_formed_xml_yields_no_defects()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "Directory.Build.props"),
                "<Project>\n  <!-- holder flows from the company value -->\n  <PropertyGroup />\n</Project>\n");
            Assert.Empty(XmlAssetValidator.Validate(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-xml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
