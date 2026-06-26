using Hx.Scaffold.Core;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T047 regression guard (fast — no install smoke): the source-free <c>hx new</c> finds the bundled template
/// pack where 007 T023 stages it, under <c>&lt;payloadRoot&gt;/templates/</c>. A version that searched only the
/// payload-root top dir missed it and fell to the dev-only <c>dotnet pack</c>, which fails with no source repo
/// (the SC-001 violation the heavy <see cref="InstallLocationSmokeTests"/> caught).
/// </summary>
public sealed class TemplateBundledPackTests
{
    [Fact]
    public void Finds_the_pack_in_the_templates_subdir_where_the_payload_stages_it()
    {
        string root = NewTempDir();
        try
        {
            string templates = Path.Combine(root, "templates");
            Directory.CreateDirectory(templates);
            string pack = Path.Combine(templates, TemplateGenerator.PackId + ".0.1.0.nupkg");
            File.WriteAllText(pack, "");

            // The installed layout: the manifest is at the payload root, the pack one level down under templates/.
            Assert.Equal(pack, TemplateGenerator.FindBundledPack(templates));
            Assert.Null(TemplateGenerator.FindBundledPack(root)); // not at the top dir — must look in templates/
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Returns_null_when_absent_so_dev_mode_falls_back_to_dotnet_pack()
    {
        string root = NewTempDir();
        try
        {
            // An empty source tree (dev/self-host) has no pre-built pack → null → the caller's source `dotnet pack`.
            Assert.Null(TemplateGenerator.FindBundledPack(Path.Combine(root, "templates")));
            Assert.Null(TemplateGenerator.FindBundledPack(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Picks_the_newest_pack_when_several_are_present()
    {
        string root = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, TemplateGenerator.PackId + ".0.1.0.nupkg"), "");
            File.WriteAllText(Path.Combine(root, TemplateGenerator.PackId + ".0.2.0.nupkg"), "");

            Assert.EndsWith("0.2.0.nupkg", TemplateGenerator.FindBundledPack(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-bundled-pack-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
