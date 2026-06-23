using System.Linq;
using System.Text.Json;
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
