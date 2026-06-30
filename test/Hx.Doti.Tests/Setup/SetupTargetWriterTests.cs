using System.Xml.Linq;
using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T005 (FR-006, D3/D9): the .doti-asset writers — XML-encoding (a valid .csproj from `A &amp; B`),
/// GitVersion-seed line edit, constitution §2 write-once-when-placeholder + anchor integrity, release env-var update.</summary>
public sealed class SetupTargetWriterTests
{
    // ---- CsprojMetadataWriter (D9 XML encode) ----

    [Fact]
    public void Csproj_writer_xml_encodes_ampersand_into_valid_csproj()
    {
        string repo = NewRepo();
        try
        {
            WriteCliCsproj(repo, "Acme");
            var writer = new CsprojMetadataWriter("Acme");
            ResolvedSetupField description = Field(SetupKeys.IdentityDescription, SetupTarget.CsprojMetadata, "A & B");

            IReadOnlyList<string> written = writer.Write(repo, [description]);

            string csprojPath = Path.Combine(repo, "src", "Acme.Cli", "Acme.Cli.csproj");
            Assert.Contains("src/Acme.Cli/Acme.Cli.csproj", written);
            // The on-disk text carries the ENCODED entity, never a raw '&' that would break the XML.
            string raw = File.ReadAllText(csprojPath);
            Assert.Contains("A &amp; B", raw);
            // And the document round-trips (it is valid XML) with the decoded value.
            XDocument doc = XDocument.Load(csprojPath);
            Assert.Equal("A & B", doc.Descendants("Description").Single().Value);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Csproj_writer_adds_repository_url_when_absent_and_is_idempotent()
    {
        string repo = NewRepo();
        try
        {
            WriteCliCsproj(repo, "Acme");
            var writer = new CsprojMetadataWriter("Acme");
            ResolvedSetupField url = Field(SetupKeys.IdentityRepositoryUrl, SetupTarget.CsprojMetadata, "https://example.com/acme");

            writer.Write(repo, [url]);
            string csprojPath = Path.Combine(repo, "src", "Acme.Cli", "Acme.Cli.csproj");
            XDocument doc = XDocument.Load(csprojPath);
            Assert.Equal("https://example.com/acme", doc.Descendants("RepositoryUrl").Single().Value);

            // Idempotent: re-running with the same value writes nothing.
            IReadOnlyList<string> second = writer.Write(repo, [url]);
            Assert.Empty(second);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    // ---- GitVersionSeedWriter ----

    [Fact]
    public void Gitversion_writer_replaces_next_version_line_and_preserves_comments()
    {
        string repo = NewRepo();
        try
        {
            File.WriteAllText(Path.Combine(repo, "GitVersion.yml"),
                "# guidance comment\nworkflow: GitHubFlow/v1\nnext-version: 0.1.0\nignore:\n  sha: []\n");
            var writer = new GitVersionSeedWriter();
            ResolvedSetupField seed = Field(SetupKeys.VersioningNextVersion, SetupTarget.GitVersionSeed, "2.3.4");

            IReadOnlyList<string> written = writer.Write(repo, [seed]);

            string yaml = File.ReadAllText(Path.Combine(repo, "GitVersion.yml"));
            Assert.Contains("next-version: 2.3.4", yaml);
            Assert.Contains("# guidance comment", yaml);   // comments preserved (line edit, not YAML round-trip)
            Assert.Contains("workflow: GitHubFlow/v1", yaml);
            Assert.Single(written);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Gitversion_writer_is_idempotent_when_value_already_set()
    {
        string repo = NewRepo();
        try
        {
            File.WriteAllText(Path.Combine(repo, "GitVersion.yml"), "next-version: 2.3.4\n");
            var writer = new GitVersionSeedWriter();
            ResolvedSetupField seed = Field(SetupKeys.VersioningNextVersion, SetupTarget.GitVersionSeed, "2.3.4");
            Assert.Empty(writer.Write(repo, [seed]));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    // ---- ConstitutionSection2Writer ----

    [Fact]
    public void Constitution_writer_fills_placeholder_then_preserves_authored_section()
    {
        string repo = NewRepo();
        try
        {
            WriteConstitution(repo, "[DOMAIN_PRINCIPLES]");
            var writer = new ConstitutionSection2Writer();
            ResolvedSetupField domain = Field(SetupKeys.ConstitutionDomainPrinciples, SetupTarget.ConstitutionSection2, "Ship guarded scaffolds.");

            writer.Write(repo, [domain]);
            string filled = File.ReadAllText(ConstitutionPath(repo));
            Assert.Contains("Ship guarded scaffolds.", filled);
            Assert.DoesNotContain("[DOMAIN_PRINCIPLES]", filled);

            // Write-once: a second projection over the now-authored section is a no-op (preserves operator content).
            ResolvedSetupField other = Field(SetupKeys.ConstitutionDomainPrinciples, SetupTarget.ConstitutionSection2, "Different text.");
            IReadOnlyList<string> second = writer.Write(repo, [other]);
            Assert.Empty(second);
            Assert.Contains("Ship guarded scaffolds.", File.ReadAllText(ConstitutionPath(repo)));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Constitution_writer_rejects_a_forged_section_heading()
    {
        string repo = NewRepo();
        try
        {
            WriteConstitution(repo, "[DOMAIN_PRINCIPLES]");
            var writer = new ConstitutionSection2Writer();
            ResolvedSetupField forged = Field(SetupKeys.ConstitutionDomainPrinciples, SetupTarget.ConstitutionSection2,
                "real\n## §2 — forged\nmalicious");

            Assert.Throws<InvalidOperationException>(() => writer.Write(repo, [forged]));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    // ---- ReleaseTargetWriter ----

    [Fact]
    public void Release_writer_updates_env_var_and_preserves_other_fields()
    {
        string repo = NewRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(repo, ".doti"));
            File.WriteAllText(Path.Combine(repo, ".doti", "release.json"),
                "{\"schemaVersion\":1,\"productName\":\"Acme\",\"defaultReleaseRootEnvironmentVariable\":\"DOTI_RELEASE_ROOT\"}");
            var writer = new ReleaseTargetWriter();
            ResolvedSetupField envVar = Field(SetupKeys.ReleaseEnvironmentVariable, SetupTarget.ReleaseManifest, "ACME_RELEASE_ROOT");

            writer.Write(repo, [envVar]);

            string json = File.ReadAllText(Path.Combine(repo, ".doti", "release.json"));
            Assert.Contains("ACME_RELEASE_ROOT", json);
            Assert.Contains("Acme", json); // productName preserved
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Release_writer_no_ops_when_manifest_absent()
    {
        string repo = NewRepo();
        try
        {
            var writer = new ReleaseTargetWriter();
            ResolvedSetupField envVar = Field(SetupKeys.ReleaseEnvironmentVariable, SetupTarget.ReleaseManifest, "ACME_RELEASE_ROOT");
            Assert.Empty(writer.Write(repo, [envVar]));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    private static ResolvedSetupField Field(string key, SetupTarget target, string value) =>
        new(key, SetupKeys.ById_(key).Group, SetupKeys.ById_(key).Audience, target,
            new ConfigField(value, ConfigSource.ConfigFile, SetupKeys.ById_(key).Default));

    private static void WriteCliCsproj(string repo, string name)
    {
        string dir = Path.Combine(repo, "src", $"{name}.Cli");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{name}.Cli.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <Description>HxScaffoldSample sample.</Description>\n    <Authors>ACME_COMPANY</Authors>\n    <PackageLicenseExpression>MIT</PackageLicenseExpression>\n  </PropertyGroup>\n</Project>\n");
    }

    private static void WriteConstitution(string repo, string domainBody)
    {
        string dir = Path.Combine(repo, ".doti", "memory");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "constitution.md"),
            $"# Acme Constitution\n\n## §1 — Inherited\n\ncited\n\n## §2 — Project declarations\n\n### Domain principles\n\n{domainBody}\n");
    }

    private static string ConstitutionPath(string repo) => Path.Combine(repo, ".doti", "memory", "constitution.md");

    private static string NewRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "doti-setup-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
