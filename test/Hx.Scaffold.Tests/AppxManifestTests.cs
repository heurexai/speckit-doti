using System.Xml.Linq;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T026: the Store MSIX manifest exposes <c>hx</c> as a CONSOLE app execution alias
/// (<c>uap5:AppExecutionAlias</c> with <c>desktop4:Subsystem="console"</c>, so an installed
/// <c>hx --help</c> / <c>hx version --json</c> attaches to the calling terminal's stdio) and declares
/// <c>runFullTrust</c>. Structural assertion over the checked-in manifest (parsed, so it must be well-formed).
/// </summary>
public sealed class AppxManifestTests
{
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap5 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";
    private static readonly XNamespace Desktop4 = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/4";
    private static readonly XNamespace Rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    private static XDocument Manifest() =>
        XDocument.Load(Path.Combine(FindRepoRoot(), "packaging", "msix", "AppxManifest.xml"));

    [Fact]
    public void Manifest_exposes_hx_as_a_console_app_execution_alias()
    {
        XDocument doc = Manifest();

        XElement alias = doc.Descendants(Uap5 + "AppExecutionAlias").Single();

        // The console subsystem attribute is what attaches the alias to the calling terminal's stdio — the whole
        // point of a CLI alias. Without it the packaged app launches detached and swallows console output.
        Assert.Equal("console", (string?)alias.Attribute(Desktop4 + "Subsystem"));

        XElement executionAlias = alias.Elements(Uap5 + "ExecutionAlias").Single();
        Assert.Equal("hx.exe", (string?)executionAlias.Attribute("Alias"));

        // The alias is wired through a uap5 appExecutionAlias extension (not the detached uap3/desktop form).
        XElement extension = (XElement)alias.Parent!;
        Assert.Equal(Uap5 + "Extension", extension.Name);
        Assert.Equal("windows.appExecutionAlias", (string?)extension.Attribute("Category"));
        Assert.Equal("hx.exe", (string?)extension.Attribute("Executable"));
    }

    [Fact]
    public void Manifest_declares_run_full_trust_and_not_the_detached_alias_form()
    {
        XDocument doc = Manifest();

        Assert.Contains(
            doc.Descendants(Rescap + "Capability"),
            c => (string?)c.Attribute("Name") == "runFullTrust");

        // The old detached GUI-style alias (uap3:AppExecutionAlias / desktop:ExecutionAlias) must be gone.
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";
        XNamespace uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
        Assert.Empty(doc.Descendants(desktop + "ExecutionAlias"));
        Assert.Empty(doc.Descendants(uap3 + "AppExecutionAlias"));

        // Sanity: the application still targets the full-trust win32 entry point and the hx executable.
        XElement app = doc.Descendants(Foundation + "Application").Single();
        Assert.Equal("hx.exe", (string?)app.Attribute("Executable"));
        Assert.Equal("Windows.FullTrustApplication", (string?)app.Attribute("EntryPoint"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output.");
    }
}
