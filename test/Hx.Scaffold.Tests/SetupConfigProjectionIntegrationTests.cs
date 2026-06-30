using System.Xml.Linq;
using Hx.Cli.Kernel;
using Hx.Doti.Core.Setup;
using Hx.Scaffold.Cli;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 029 T016/T019 (SC-001/SC-006/SC-007/SC-008, D6/D9/D10): end-to-end integration for the <c>hx new</c> setup-config
/// path. These drive the REAL command (<see cref="ScaffoldCommands.New"/>) for validate-before-generate / fail-closed /
/// no-op behaviour, and the REAL post-generation projection pipeline (the exact
/// <see cref="SetupConfigProjector"/> + <see cref="SetupTargetWriters.ForNew"/> + <see cref="SetupConfigStore"/> units
/// <c>ScaffoldNewRunner</c> invokes) against a generated-repo skeleton — so SC-001/006/007/008 are covered without the
/// multi-minute <c>dotnet new</c> + smoke (that heavy path stays in <c>ScaffoldNewSmokeTests</c>, gated by HX_SCAFFOLD_SMOKE).
/// </summary>
public sealed class SetupConfigProjectionIntegrationTests
{
    private static readonly CliMeta Meta = new("hx", "0.0.0-test");
    private const string ProjectName = "Acme.Widget";

    // ---- SC-001: a --config repo's project files match the config; an omitted field takes its documented default ----

    [Fact]
    public void Config_projects_into_csproj_gitversion_release_and_constitution()
    {
        string repo = NewGeneratedRepoSkeleton(ProjectName);
        try
        {
            // The same config an agent would hand `hx new --config`, resolved + projected exactly as ScaffoldNewRunner does.
            const string config = """
            {
              "schemaVersion": 1,
              "identity": {
                "name": "Acme.Widget",
                "company": "Acme",
                "description": "The Acme widget CLI.",
                "repositoryUrl": "https://example.com/acme/widget",
                "license": "Apache-2.0"
              },
              "versioning": { "nextVersion": "2.3.4" },
              "release": { "environmentVariable": "ACME_RELEASE_ROOT" },
              "constitution": { "domainPrinciples": "Ship guarded widgets." }
            }
            """;
            ResolvedSetupConfig resolved = ResolveFromJson(config, SetupAudience.New);
            ProjectAndPersist(repo, resolved);

            // .csproj metadata (Description / RepositoryUrl / PackageLicenseExpression) match the config.
            XDocument csproj = XDocument.Load(CliCsproj(repo, ProjectName));
            Assert.Equal("The Acme widget CLI.", csproj.Descendants("Description").Single().Value);
            Assert.Equal("https://example.com/acme/widget", csproj.Descendants("RepositoryUrl").Single().Value);
            Assert.Equal("Apache-2.0", csproj.Descendants("PackageLicenseExpression").Single().Value);

            // GitVersion.yml next-version + release.json env-var + constitution §2 match the config.
            Assert.Contains("next-version: 2.3.4", File.ReadAllText(Path.Combine(repo, "GitVersion.yml")));
            Assert.Contains("ACME_RELEASE_ROOT", File.ReadAllText(Path.Combine(repo, ".doti", "release.json")));
            string constitution = File.ReadAllText(ConstitutionPath(repo));
            Assert.Contains("Ship guarded widgets.", constitution);
            Assert.DoesNotContain("[DOMAIN_PRINCIPLES]", constitution);

            // SC-001 (default): an OMITTED field keeps its template default — versioning supplied authors? no. The
            // §2 sections the config did not set keep their placeholder (the template default).
            Assert.Contains("[TECH_STACK]", constitution);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Omitted_csproj_field_keeps_the_template_default()
    {
        string repo = NewGeneratedRepoSkeleton(ProjectName);
        try
        {
            // Only the license is supplied; <Description> must keep the template's sample sentence (the documented default).
            ResolvedSetupConfig resolved = ResolveFromJson(
                """{ "schemaVersion": 1, "identity": { "license": "MIT" } }""", SetupAudience.New);
            ProjectAndPersist(repo, resolved);

            XDocument csproj = XDocument.Load(CliCsproj(repo, ProjectName));
            Assert.Equal(SetupConfigDefaults.Description, csproj.Descendants("Description").Single().Value);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    // ---- SC-006 / D9: encoding + containment + fail-closed validation ----

    [Fact]
    public void Ampersand_description_in_config_is_rejected_before_generation()
    {
        // D9/D5 (defence #1): a free-text identity field carrying an XML metacharacter ('&') is rejected by the real
        // command at validation — fail-closed, naming the field, leaving NO partial repo (SC-006). The on-disk encode
        // (defence #2) is exercised separately below for any value that legitimately reaches the writer.
        string sandbox = NewTempDir("hx-new-amp-");
        string output = Path.Combine(sandbox, ProjectName);
        try
        {
            string configPath = WriteConfig(sandbox, """{ "schemaVersion": 1, "identity": { "description": "A & B" } }""");
            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(sandbox);
                CliResult result = ScaffoldCommands.New(
                    Meta, name: ProjectName, company: "Acme", output: output, profile: "dotnet-cli",
                    agentsCsv: "codex,claude", configPath: Path.GetFileName(configPath));

                Assert.False(result.Ok);
                Assert.Equal((int)ExitClass.Validation, result.ExitCode);
                Assert.Contains(result.Errors, e => e.Target == "identity.description");
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCwd);
            }

            Assert.False(Directory.Exists(output), "a rejected config must leave no partial repo");
        }
        finally
        {
            ForceDelete(sandbox);
        }
    }

    [Fact]
    public void Csproj_writer_xml_encodes_a_value_that_reaches_it_into_valid_csproj()
    {
        // D9 (defence #2): when a value WITH an '&' reaches the csproj writer (the projector path ScaffoldNewRunner
        // uses), it is XML-ENCODED via XDocument so the .csproj stays valid XML and `dotnet build` would parse it.
        string repo = NewGeneratedRepoSkeleton(ProjectName);
        try
        {
            // Drive the projector with a resolved field directly (bypassing the field's free-text schema guard) to
            // prove the writer-level encode — exactly the belt-and-suspenders D9 guarantee.
            var fields = new List<ResolvedSetupField>
            {
                new(SetupKeys.IdentityDescription, SetupGroup.Identity, SetupAudience.New, SetupTarget.CsprojMetadata,
                    new ConfigField("A & B", ConfigSource.ConfigFile, SetupConfigDefaults.Description)),
            };
            var resolved = new ResolvedSetupConfig(1, SetupAudience.New, fields);
            SetupConfigProjector.Project(resolved, repo, SetupTargetWriters.ForNew(ProjectName));

            string raw = File.ReadAllText(CliCsproj(repo, ProjectName));
            Assert.Contains("A &amp; B", raw);                 // encoded on disk — never a raw '&' that breaks MSBuild
            XDocument csproj = XDocument.Load(CliCsproj(repo, ProjectName)); // valid XML, round-trips to the decoded value
            Assert.Equal("A & B", csproj.Descendants("Description").Single().Value);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void New_with_traversal_output_is_rejected_and_creates_nothing()
    {
        // D9: `--output ../escape` (a `..` traversal) is rejected by the real command before generation — no files.
        string sandbox = NewTempDir("hx-new-escape-");
        string escapeTarget = Path.Combine(Path.GetDirectoryName(sandbox)!, "hx-new-escaped-" + Guid.NewGuid().ToString("n"));
        string previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(sandbox);
            CliResult result = ScaffoldCommands.New(
                Meta, name: "Acme.Widget", company: "Acme", output: Path.Combine("..", Path.GetFileName(escapeTarget)),
                profile: "dotnet-cli", agentsCsv: "codex,claude");

            Assert.False(result.Ok);
            Assert.Equal((int)ExitClass.Validation, result.ExitCode);
            Assert.Contains(result.Errors, e => e.Message.Contains("traversal", StringComparison.OrdinalIgnoreCase));
            Assert.False(Directory.Exists(escapeTarget), "a rejected traversal output must create no repo");
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            ForceDelete(sandbox);
            ForceDelete(escapeTarget);
        }
    }

    [Fact]
    public void New_with_invalid_agent_fails_closed_naming_the_field_and_creates_nothing()
    {
        string sandbox = NewTempDir("hx-new-badagent-");
        string output = Path.Combine(sandbox, "Acme.Widget");
        try
        {
            string configPath = WriteConfig(sandbox, """{ "schemaVersion": 1, "agents": ["gemini"] }""");
            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(sandbox);
                CliResult result = ScaffoldCommands.New(
                    Meta, name: "Acme.Widget", company: "Acme", output: output, profile: "dotnet-cli",
                    agentsCsv: "codex,claude", configPath: Path.GetFileName(configPath));

                Assert.False(result.Ok);
                Assert.Equal((int)ExitClass.Validation, result.ExitCode);
                Assert.Contains(result.Errors, e => e.Target == "agents");
                Assert.Contains(result.Errors, e => e.Message.Contains("gemini", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCwd);
            }

            Assert.False(Directory.Exists(output), "an invalid config must leave no partial repo (SC-006)");
        }
        finally
        {
            ForceDelete(sandbox);
        }
    }

    [Fact]
    public void New_with_wrong_schema_version_fails_closed_naming_the_field()
    {
        string sandbox = NewTempDir("hx-new-badschema-");
        string output = Path.Combine(sandbox, "Acme.Widget");
        try
        {
            string configPath = WriteConfig(sandbox, """{ "schemaVersion": 2 }""");
            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(sandbox);
                CliResult result = ScaffoldCommands.New(
                    Meta, name: "Acme.Widget", company: "Acme", output: output, profile: "dotnet-cli",
                    agentsCsv: "codex,claude", configPath: Path.GetFileName(configPath));

                Assert.False(result.Ok);
                Assert.Equal((int)ExitClass.Validation, result.ExitCode);
                Assert.Contains(result.Errors, e => e.Target == "schemaVersion");
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCwd);
            }

            Assert.False(Directory.Exists(output));
        }
        finally
        {
            ForceDelete(sandbox);
        }
    }

    // ---- SC-007 / D10: provable no-op fence (no --config / --interactive) ----

    [Fact]
    public void No_config_projection_writes_nothing_and_persists_no_setup_json()
    {
        // D10: the exact post-generation call ScaffoldNewRunner makes when request.Setup is null touches no writer and
        // leaves the generated tree byte-identical (no .doti/setup.json, csproj/GitVersion/release/constitution unchanged).
        string repo = NewGeneratedRepoSkeleton(ProjectName);
        try
        {
            string csprojBefore = File.ReadAllText(CliCsproj(repo, ProjectName));
            string gitVersionBefore = File.ReadAllText(Path.Combine(repo, "GitVersion.yml"));
            string releaseBefore = File.ReadAllText(Path.Combine(repo, ".doti", "release.json"));
            string constitutionBefore = File.ReadAllText(ConstitutionPath(repo));

            SetupProjectionResult result = SetupConfigProjector.Project(
                null, repo, SetupTargetWriters.ForNew(ProjectName));

            Assert.Empty(result.Written);
            Assert.False(File.Exists(Path.Combine(repo, ".doti", "setup.json")));
            Assert.Equal(csprojBefore, File.ReadAllText(CliCsproj(repo, ProjectName)));
            Assert.Equal(gitVersionBefore, File.ReadAllText(Path.Combine(repo, "GitVersion.yml")));
            Assert.Equal(releaseBefore, File.ReadAllText(Path.Combine(repo, ".doti", "release.json")));
            Assert.Equal(constitutionBefore, File.ReadAllText(ConstitutionPath(repo)));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    // ---- SC-008: the FR-007 checklist is present and inert ----

    [Fact]
    public void Checklist_for_publish_intent_names_operator_only_and_deferred_steps_and_is_inert()
    {
        // SC-008: a publish-target config yields a checklist that NAMES the operator-only OIDC steps + the 030 git/CI
        // steps; it is pure inert text (building it touches nothing on disk / no nuget / no git).
        var publish = new SetupPublishIntent(true, "acme", "widget", "release.yml", "production", "nuget.org");

        IReadOnlyList<CliNextAction> checklist = SetupChecklist.AsNextActions(publish);
        string joined = string.Join("\n", checklist.Select(a => $"{a.Label} :: {a.Why}"));

        // operator-only steps, naming the OIDC owner/repo/workflow/environment (FR-007).
        Assert.Contains(checklist, a => a.Label.Contains("Trusted-Publishing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("owner: acme", joined);
        Assert.Contains("repo: widget", joined);
        Assert.Contains("workflow: release.yml", joined);
        Assert.Contains("environment: production", joined);
        Assert.Contains(checklist, a => a.Label.Contains("NUGET_USER", StringComparison.Ordinal));
        Assert.Contains(checklist, a => a.Label.Contains("branch protection", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(checklist, a => a.Label.Contains("`v*` release tag", StringComparison.Ordinal));

        // the git/CI steps deferred to 030.
        Assert.Contains(checklist, a => a.Label.Contains("baseline commit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(checklist, a => a.Label.Contains(".github/workflows", StringComparison.Ordinal));
        Assert.Contains(checklist, a => a.Label.Contains("DCO", StringComparison.Ordinal));

        // every item is tagged with one of the two never-executed categories.
        Assert.All(checklist, a => Assert.True(
            a.Label.Contains(SetupChecklist.CategoryOperatorOnly, StringComparison.Ordinal)
            || a.Label.Contains(SetupChecklist.CategoryDeferred030, StringComparison.Ordinal)));
    }

    [Fact]
    public void Checklist_omits_nuget_oidc_items_when_publish_is_not_intended()
    {
        IReadOnlyList<CliNextAction> checklist = SetupChecklist.AsNextActions(SetupPublishIntent.None);

        Assert.DoesNotContain(checklist, a => a.Label.Contains("Trusted-Publishing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(checklist, a => a.Label.Contains("NUGET_USER", StringComparison.Ordinal));
        // the non-publish operator + 030 steps still appear (they are not publish-gated).
        Assert.Contains(checklist, a => a.Label.Contains("branch protection", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(checklist, a => a.Label.Contains("baseline commit", StringComparison.OrdinalIgnoreCase));
    }

    // ---- D6: machine-local localOutput.directory never lands in the tracked .doti/setup.json ----

    [Fact]
    public void Machine_local_release_directory_is_never_written_into_tracked_setup_json()
    {
        string repo = NewGeneratedRepoSkeleton(ProjectName);
        try
        {
            // release.directory + release.enabled are machine-local; the env-var name is repo-portable.
            ResolvedSetupConfig resolved = ResolveFromJson(
                """
                {
                  "schemaVersion": 1,
                  "identity": { "license": "Apache-2.0" },
                  "release": { "environmentVariable": "ACME_ROOT", "directory": "C:\\machine\\out", "enabled": false }
                }
                """,
                SetupAudience.New);
            ProjectAndPersist(repo, resolved);

            string json = File.ReadAllText(Path.Combine(repo, ".doti", "setup.json"));
            Assert.DoesNotContain("C:\\\\machine\\\\out", json);
            Assert.DoesNotContain("machine", json);
            Assert.DoesNotContain("\"directory\"", json);
            Assert.DoesNotContain("\"enabled\"", json);
            Assert.Contains("ACME_ROOT", json);                 // the portable env-var name is kept
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    // ---- helpers ----

    private static ResolvedSetupConfig ResolveFromJson(string json, SetupAudience audience)
    {
        SetupValidationResult validation = SetupConfigSchema.ValidateRaw(json, out SetupConfig? config);
        Assert.True(validation.Ok, string.Join("; ", validation.Errors.Select(e => $"{e.Field}: {e.Message}")));
        return SetupConfigResolver.Resolve(config, flags: null, audience);
    }

    /// <summary>Run the exact post-generation projection + persistence ScaffoldNewRunner performs for a non-null Setup.</summary>
    private static void ProjectAndPersist(string repo, ResolvedSetupConfig resolved)
    {
        SetupConfigProjector.Project(resolved, repo, SetupTargetWriters.ForNew(ProjectName));
        SetupConfigStore.WriteFromResolved(repo, resolved);
    }

    private static string CliCsproj(string repo, string name) =>
        Path.Combine(repo, "src", $"{name}.Cli", $"{name}.Cli.csproj");

    private static string ConstitutionPath(string repo) =>
        Path.Combine(repo, ".doti", "memory", "constitution.md");

    /// <summary>A minimal generated-repo skeleton with exactly the files the new-path projector targets (the CLI
    /// .csproj, GitVersion.yml, .doti/release.json, and the §1/§2 constitution with unfilled placeholders) — the shape
    /// the template + installer leave before ScaffoldNewRunner runs the setup projection.</summary>
    private static string NewGeneratedRepoSkeleton(string name)
    {
        string repo = NewTempDir("hx-new-setup-");

        string cliDir = Path.Combine(repo, "src", $"{name}.Cli");
        Directory.CreateDirectory(cliDir);
        File.WriteAllText(Path.Combine(cliDir, $"{name}.Cli.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n" +
            $"    <Description>{SetupConfigDefaults.Description}</Description>\n" +
            "    <Authors>Heurex</Authors>\n    <PackageLicenseExpression>MIT</PackageLicenseExpression>\n" +
            "  </PropertyGroup>\n</Project>\n");

        File.WriteAllText(Path.Combine(repo, "GitVersion.yml"),
            "# GitVersion guidance comment (preserved)\nworkflow: GitHubFlow/v1\nnext-version: 0.1.0\n");

        Directory.CreateDirectory(Path.Combine(repo, ".doti"));
        File.WriteAllText(Path.Combine(repo, ".doti", "release.json"),
            "{\"schemaVersion\":1,\"productName\":\"" + name + "\",\"defaultReleaseRootEnvironmentVariable\":\"DOTI_RELEASE_ROOT\"}");

        Directory.CreateDirectory(Path.Combine(repo, ".doti", "memory"));
        File.WriteAllText(ConstitutionPath(repo),
            $"# {name} Constitution\n\n## §1 — Inherited\n\ncited baseline\n\n## §2 — Project declarations\n\n" +
            "### Domain principles\n\n[DOMAIN_PRINCIPLES]\n\n### Tech stack\n\n[TECH_STACK]\n\n" +
            "### Coding style\n\n[CODING_STYLE]\n\n### Security & compliance\n\n[SECURITY_COMPLIANCE]\n\n" +
            "### Performance\n\n[PERFORMANCE]\n");

        return repo;
    }

    private static string WriteConfig(string dir, string json)
    {
        string path = Path.Combine(dir, "setup.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string NewTempDir(string prefix)
    {
        string dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
