using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Hx.Templates.Tests;

/// <summary>
/// Fast, always-on structural checks on the template assets (the gate). The heavy
/// pack/install/instantiate/build round-trip lives in <see cref="TemplateRoundTripTests"/>.
/// </summary>
public sealed class TemplateGoldenTests
{
    [Fact]
    public void Template_config_has_expected_identity_and_symbols()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(TemplateRepo.TemplateConfig));
        JsonElement root = doc.RootElement;

        Assert.Equal("HxScaffoldSample", root.GetProperty("sourceName").GetString());
        Assert.Equal("hx-dotnet-cli", root.GetProperty("shortName").GetString());

        JsonElement symbols = root.GetProperty("symbols");
        Assert.True(symbols.TryGetProperty("includeArchitectureTests", out JsonElement arch));
        Assert.Equal("bool", arch.GetProperty("datatype").GetString());
        Assert.Equal("true", arch.GetProperty("defaultValue").GetString());
        Assert.True(symbols.TryGetProperty("company", out _));
        Assert.True(symbols.TryGetProperty("framework", out _));
    }

    [Fact]
    public void Template_config_excludes_arch_project_when_opted_out()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(TemplateRepo.TemplateConfig));
        // A sources modifier must drop the arch-test project files when includeArchitectureTests is false.
        string json = doc.RootElement.GetProperty("sources").GetRawText();
        Assert.Contains("includeArchitectureTests", json);
        Assert.Contains("HxScaffoldSample.Architecture.Tests", json);
    }

    [Fact]
    public void Slnx_conditionally_references_the_arch_project()
    {
        string slnx = File.ReadAllText(TemplateRepo.Slnx);
        Assert.Contains("<!--#if (includeArchitectureTests) -->", slnx);
        Assert.Contains("HxScaffoldSample.Architecture.Tests/HxScaffoldSample.Architecture.Tests.csproj", slnx);
        Assert.Contains("<!--#endif -->", slnx);
    }

    [Fact]
    public void Architecture_tests_define_the_structural_and_security_families()
    {
        string src = File.ReadAllText(TemplateRepo.ArchTests)
            + "\n"
            + File.ReadAllText(TemplateRepo.ArchCapabilityTests);
        foreach (string family in new[]
        {
            "Namespace dependency", "Class dependency", "Inheritance naming",
            "namespace containment", "Attribute access", "Cycle", "Security architecture",
            "Output confinement", "CLI surface confinement",
        })
        {
            Assert.Contains(family, src);
        }

        // Six structural families (five with a negative fixture; cycle is positive-only) plus the security
        // family (positive + non-vacuity guard), the agent-first output-confinement family (positive +
        // non-vacuity guard), and the CLI surface-confinement family (positive + negative fixture)
        // = 17 facts, 6 negative fixtures.
        int facts = CountOccurrences(src, "[Fact]");
        int negatives = CountOccurrences(src, "Negative_");
        Assert.Equal(17, facts);
        Assert.Equal(6, negatives);
    }

    [Fact]
    public void Architecture_contract_matches_the_layering_and_families()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(TemplateRepo.ArchitectureJson));
        JsonElement root = doc.RootElement;

        // The library must not depend on the CLI (the core direction the arch tests enforce).
        JsonElement library = root.GetProperty("layers").GetProperty("library");
        Assert.Empty(library.GetProperty("mayDependOn").EnumerateArray());

        // The contract names exactly the families that the ArchUnitNET tests implement.
        var ids = root.GetProperty("families").EnumerateArray()
            .Select(f => f.GetProperty("id").GetString()).ToList();
        Assert.Equal(9, ids.Count);
        foreach (string id in new[]
        {
            "namespaceDependency", "classDependency", "inheritanceNaming",
            "classNamespaceContainment", "attributeAccess", "cycle", "capabilityConfinement",
            "outputConfinement", "cliSurfaceConfinement",
        })
        {
            Assert.Contains(id, ids);
        }
    }

    [Fact]
    public void Pack_project_is_a_template_package_that_does_not_compile()
    {
        string proj = File.ReadAllText(TemplateRepo.PackProject);
        Assert.Contains("<PackageType>Template</PackageType>", proj);
        Assert.Contains("<EnableDefaultItems>false</EnableDefaultItems>", proj);
        Assert.Contains("Include=\"templates\\**\\*\"", proj);
        Assert.Contains("<Compile Remove=\"**\\*\" />", proj);
        Assert.Contains("<DisableGitVersionTask>true</DisableGitVersionTask>", proj);

        string props = File.ReadAllText(TemplateRepo.DirectoryBuildProps);
        Assert.Contains("Condition=\"'$(MSBuildProjectName)' != 'Hx.Scaffold.Templates'\"", props);
        Assert.Contains("<PackageReference Include=\"GitVersion.MsBuild\"", props);
    }

    [Fact]
    public void Template_ships_runner_policy_configs()
    {
        // The generated repo's hygiene + Sentrux policy, consumed by the vendored runner.
        // .sentrux/rules.toml must agree with rules/architecture.json
        // (library must not depend on the CLI). Vendored tooling under tools/ is excluded from scans.
        Assert.True(File.Exists(TemplateRepo.HygieneJson), "template must ship rules/hygiene.json");
        Assert.True(File.Exists(TemplateRepo.SentruxJson), "template must ship rules/sentrux.json");
        Assert.True(File.Exists(TemplateRepo.SentruxRulesToml), "template must ship .sentrux/rules.toml");
        Assert.True(File.Exists(TemplateRepo.SentruxIgnore), "template must ship .sentruxignore");

        using JsonDocument sentrux = JsonDocument.Parse(File.ReadAllText(TemplateRepo.SentruxJson));
        Assert.True(sentrux.RootElement.GetProperty("firstSmokeBaseline").GetBoolean());
        Assert.Equal("Heurex fork", sentrux.RootElement.GetProperty("forkStamp").GetString());

        using JsonDocument hygiene = JsonDocument.Parse(File.ReadAllText(TemplateRepo.HygieneJson));
        var excludes = hygiene.RootElement.GetProperty("excludePaths").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains("tools", excludes); // vendored tooling excluded from the generated repo's scan

        string toml = File.ReadAllText(TemplateRepo.SentruxRulesToml);
        Assert.Contains("name = \"library\"", toml);
        Assert.Contains("name = \"cli\"", toml);
        Assert.Contains("Domain library must not depend on the CLI", toml); // matches architecture.json

        Assert.Contains("tools/", File.ReadAllText(TemplateRepo.SentruxIgnore));
    }

    [Fact]
    public void Template_ships_git_hygiene_files()
    {
        // The generated repo must come with .gitignore + .gitattributes so the first commit is clean
        // (no bin/obj or vendored binaries staged) and line endings are pinned across platforms.
        Assert.True(File.Exists(TemplateRepo.GitIgnore), "template must ship .gitignore");
        Assert.True(File.Exists(TemplateRepo.GitAttributes), "template must ship .gitattributes");

        string ignore = File.ReadAllText(TemplateRepo.GitIgnore);
        Assert.Contains("bin/", ignore);
        Assert.Contains("obj/", ignore);
        Assert.Contains("tools/sentrux/bin/", ignore); // vendored binaries are not committed

        Assert.Contains("eol=lf", File.ReadAllText(TemplateRepo.GitAttributes));
    }

    [Fact]
    public void Template_does_not_ship_legacy_root_doti_payload_and_ignores_doti_runtime_state()
    {
        Assert.False(Directory.Exists(Path.Combine(TemplateRepo.TemplateDir, "doti")),
            "Doti payload is installed by the scaffold finisher into .doti, not baked into the static template root.");
        Assert.False(Directory.Exists(Path.Combine(TemplateRepo.TemplateDir, ".doti")),
            "Doti payload is installed by the scaffold finisher so the generated repo receives the current numbered workflow payload.");

        string ignore = File.ReadAllText(TemplateRepo.GitIgnore);
        Assert.Contains(".doti/cycle-state.json", ignore);
        Assert.Contains(".doti/gate-proof.json", ignore);
    }

    [Fact]
    public void Template_cli_is_agent_first_and_declares_the_envelope_contract()
    {
        // Always-on guard: the generated CLI renders through the Agent host (returns CliResult,
        // adds describe), and the envelope declares every field the published schema requires — so a generated
        // `greet --json` validates against schemas/cli-envelope.schema.json (proven end-to-end in the round-trip).
        string cliDir = Path.Combine(TemplateRepo.TemplateDir, "src", "HxScaffoldSample.Cli");
        string program = File.ReadAllText(Path.Combine(cliDir, "Program.cs"));
        Assert.Contains("Agent.Run", program);
        Assert.Contains("Agent.Invoke", program);
        Assert.Contains("Agent.Ok", program);
        Assert.Contains("\"describe\"", program);

        string agent = File.ReadAllText(Path.Combine(cliDir, "Agent.cs"));
        Assert.Contains("record CliResult", agent);
        Assert.Contains("HelpMode", agent);
        Assert.Contains("--help-mode <auto|rich|plain>", agent);
        Assert.Contains("RenderPlainHelp", agent);
        foreach (string field in new[]
        {
            "SchemaVersion", "Tool", "Version", "Command", "Outcome", "Ok", "ExitCode", "Summary",
            "Errors", "Warnings", "Info", "NextActions", "RequiresOperator", "ElapsedMs",
        })
        {
            Assert.Contains(field, agent);
        }
    }

    [Fact]
    public void Template_cli_uses_executable_adjacent_microsoft_configuration()
    {
        string cliDir = Path.Combine(TemplateRepo.TemplateDir, "src", "HxScaffoldSample.Cli");
        string packages = File.ReadAllText(Path.Combine(TemplateRepo.TemplateDir, "Directory.Packages.props"));
        string project = File.ReadAllText(Path.Combine(cliDir, "HxScaffoldSample.Cli.csproj"));
        string program = File.ReadAllText(Path.Combine(cliDir, "Program.cs"));
        string configuration = File.ReadAllText(Path.Combine(cliDir, "AppConfiguration.cs"));

        Assert.True(File.Exists(Path.Combine(cliDir, "HxScaffoldSample.Cli.config.json")));
        Assert.Contains("Microsoft.Extensions.Configuration", packages);
        Assert.Contains("Microsoft.Extensions.Configuration.Json", packages);
        Assert.Contains("PackageReference Include=\"Microsoft.Extensions.Configuration\"", project);
        Assert.Contains("PackageReference Include=\"Microsoft.Extensions.Configuration.Json\"", project);
        Assert.Contains("CopyToOutputDirectory=\"PreserveNewest\"", project);
        Assert.Contains("CopyToPublishDirectory=\"Always\"", project);
        Assert.Contains("AppConfiguration.LoadRequired(AppContext.BaseDirectory)", program);
        Assert.Contains("ConfigurationBuilder", configuration);
        Assert.Contains("AddJsonFile(FileName", configuration);
        Assert.Contains("Required application configuration file was not found next to the executable", configuration);
    }

    [Fact]
    public void Template_cli_is_packable_as_a_dotnet_global_tool_with_no_velopack()
    {
        // 007 T042 (FR-025): the scaffolded app inherits the distribution-packaging rules — it packs as a
        // framework-dependent .NET global tool (so installed commands run source-free) with no Velopack stub.
        string cliDir = Path.Combine(TemplateRepo.TemplateDir, "src", "HxScaffoldSample.Cli");
        string project = File.ReadAllText(Path.Combine(cliDir, "HxScaffoldSample.Cli.csproj"));

        Assert.Contains("<PackAsTool>true</PackAsTool>", project);
        Assert.Contains("<ToolCommandName>", project);
        Assert.Contains("<PackageId>", project);
        Assert.Contains("<IsPackable>true</IsPackable>", project);

        // Nothing packs by default; only the CLI opts back in (so `dotnet pack` ships the one tool, not the library/tests).
        string templateProps = File.ReadAllText(Path.Combine(TemplateRepo.TemplateDir, "Directory.Build.props"));
        Assert.Contains("<IsPackable>false</IsPackable>", templateProps);

        // No real Velopack stub — a package reference, a VelopackApp startup hook, or a vpk invocation. (Like the
        // source repo's no-velopack gate, match actual USAGE, not the bare word: a comment documenting "no Velopack
        // stub" is fine.) The source repo dropped Velopack; the template inherits that — installed commands update
        // via the tool channel, not a self-update hook.
        string packages = File.ReadAllText(Path.Combine(TemplateRepo.TemplateDir, "Directory.Packages.props"));
        string program = File.ReadAllText(Path.Combine(cliDir, "Program.cs"));
        foreach (string buildFile in new[] { project, templateProps, packages })
        {
            Assert.DoesNotContain("Include=\"Velopack", buildFile, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("vpk ", buildFile, System.StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("VelopackApp", program, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Template_ships_trunk_based_versioning_config()
    {
        // 020 (FR-001/004/005): a generated repo must version deterministically out of the box —
        // trunk-based so the default bump is PATCH (a doti bug cycle -> patch), and the series starts at 0.1.0.
        // Without this file a generated repo falls back to GitVersion's GitFlow default (a dev branch -> minor).
        string gitVersion = Path.Combine(TemplateRepo.TemplateDir, "GitVersion.yml");
        Assert.True(File.Exists(gitVersion), "template must ship GitVersion.yml so a generated repo versions deterministically");

        string gv = File.ReadAllText(gitVersion);
        Assert.Contains("workflow: GitHubFlow/v1", gv); // trunk 'main' increments Patch by default; no minor-tracking develop
        Assert.Contains("next-version: 0.1.0", gv);     // the version series starts at 0.1.0
    }

    [Fact]
    public void Template_ships_auto_year_company_copyright()
    {
        // 020 (FR-006/007/008): a generated release assembly must carry a company copyright whose year is
        // the year the release was produced (auto-updating), with the holder flowing from the --company value.
        string props = File.ReadAllText(Path.Combine(TemplateRepo.TemplateDir, "Directory.Build.props"));
        Assert.Contains("<Copyright>", props);
        Assert.Contains("$([System.DateTime]::UtcNow.Year)", props); // build-time year = the release year
        Assert.Contains("$(Company)", props);                        // holder == the --company value
    }

    [Fact]
    public void Every_template_xml_asset_is_well_formed_xml()
    {
        // Regression guard: the 020 Copyright comment once contained "--company"; "--" is illegal inside an XML
        // comment, so the GENERATED Directory.Build.props failed to parse, TargetFramework was never set, and every
        // generated repo failed to build (NETSDK1013) — caught only in a release smoke. The other golden tests read
        // these assets as strings, never as XML, so the gate stayed green. Parse every shipped XML asset here so an
        // XML-invalid template (a "--" in a comment, an unescaped entity) fails fast in the gate, not downstream.
        string[] xml = Directory.EnumerateFiles(TemplateRepo.TemplateDir, "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".csproj", System.StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".props", System.StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".slnx", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(xml); // guard: the glob must actually find the shipped assets

        foreach (string path in xml)
        {
            System.Exception? failure = Record.Exception(() => XDocument.Parse(File.ReadAllText(path)));
            Assert.True(
                failure is null,
                $"template XML asset is not well-formed XML and would break the generated build: {path}\n{failure?.Message}");
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, index = 0;
        while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
