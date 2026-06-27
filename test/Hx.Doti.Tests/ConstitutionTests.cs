using Hx.Doti.Core;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>009 constitution coverage: the §1/§2 template + this repo's constitution structure (T001),
/// <see cref="ProjectNameResolver"/> (T004), <see cref="ConstitutionService"/> read + §2 extraction (T006/T010),
/// and <see cref="ConstitutionInitializer"/> initialize-vs-preserve (T015).</summary>
public sealed class ConstitutionTests
{
    private static readonly string[] Section2Placeholders =
        ["[DOMAIN_PRINCIPLES]", "[TECH_STACK]", "[CODING_STYLE]", "[SECURITY_COMPLIANCE]", "[PERFORMANCE]"];

    // ---- T001 / FR-002, FR-004, FR-011, SC-001, SC-005, SC-008 : structure of the template + this repo's constitution

    [Fact]
    public void Template_has_two_layers_title_token_and_only_section2_placeholders()
    {
        string repo = FindRepoRoot();
        string template = File.ReadAllText(Path.Combine(repo, ".doti", "core", "templates", "constitution-template.md"));

        Assert.Contains("{PROJECT_NAME}", template);          // auto-filled title token (FR-015), not a placeholder
        Assert.DoesNotContain("[PROJECT_NAME]", template);     // never a bracket placeholder for the name (SC-010)
        Assert.Contains("## §1", template);
        Assert.Contains("## §2", template);
        foreach (string placeholder in Section2Placeholders)
        {
            Assert.Contains(placeholder, template);            // the five §2 placeholders exist
        }

        // FR-004 / SC-008: NO fillable placeholder for any §1 invariant (versioning / CLI shape / quality-gate / workflow).
        foreach (string forbidden in new[] { "[VERSION", "[VERSIONING", "[CLI", "[OUTPUT", "[GATE", "[QUALITY_GATE", "[WORKFLOW" })
        {
            Assert.DoesNotContain(forbidden, template);
        }
    }

    [Fact]
    public void This_repo_constitution_has_no_placeholder_tokens_and_keeps_nine_principles()
    {
        string repo = FindRepoRoot();
        string constitution = File.ReadAllText(Path.Combine(repo, ".doti", "memory", "constitution.md"));

        Assert.DoesNotContain("{PROJECT_NAME}", constitution);
        Assert.DoesNotContain("[PROJECT_NAME]", constitution);
        foreach (string placeholder in Section2Placeholders)
        {
            Assert.DoesNotContain(placeholder, constitution); // a real, filled constitution (SC-005) — no leftover brackets
        }

        Assert.Contains("## §1", constitution);
        Assert.Contains("## §2", constitution);
        foreach (string principle in new[]
        {
            "Deterministic Ownership", "Bootstrap Honesty", "Template Boundary", "Public Hygiene",
            "Cross-Platform", "Engineering Discipline", "Operator Decisions", "Codified Cycle", "Channel Independence",
        })
        {
            Assert.Contains(principle, constitution);          // the 9 §1 invariants retained (FR-011)
        }
    }

    // ---- T004 / FR-015, SC-010 : project name resolution

    [Fact]
    public void ProjectNameResolver_prefers_explicit_then_solution_then_directory()
    {
        string temp = NewTempDir();
        try
        {
            // explicit wins even with a solution present
            File.WriteAllText(Path.Combine(temp, "Foo.slnx"), "<Solution />");
            Assert.Equal("Acme.Widget", ProjectNameResolver.Resolve(temp, "Acme.Widget"));
            // single solution wins over the dir name
            Assert.Equal("Foo", ProjectNameResolver.Resolve(temp, null));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void ProjectNameResolver_falls_back_to_directory_when_no_single_solution()
    {
        string temp = NewTempDir();
        try
        {
            // zero solutions -> dir name
            Assert.Equal(Path.GetFileName(temp), ProjectNameResolver.Resolve(temp, null));
            // two solutions of the same extension -> ambiguous -> dir name
            File.WriteAllText(Path.Combine(temp, "A.sln"), "");
            File.WriteAllText(Path.Combine(temp, "B.sln"), "");
            Assert.Equal(Path.GetFileName(temp), ProjectNameResolver.Resolve(temp, null));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void ProjectNameResolver_resolves_this_repo_to_solution_name()
    {
        Assert.Equal("scaffold-dotnet", ProjectNameResolver.Resolve(FindRepoRoot(), null));
    }

    // ---- T006 / FR-006, FR-016, SC-003 : read + absence (surface-and-proceed, never throws)

    [Fact]
    public void Read_this_repo_returns_exists_with_verbatim_section2()
    {
        string repo = FindRepoRoot();
        ConstitutionReadResult result = ConstitutionService.Read(repo);

        Assert.True(result.Exists);
        Assert.NotNull(result.Section2Content);
        Assert.StartsWith("## §2", result.Section2Content!.TrimStart());
        // SC-003: the §2 emit is a VERBATIM substring of the on-disk file.
        string onDisk = File.ReadAllText(Path.Combine(repo, ".doti", "memory", "constitution.md"));
        Assert.Contains(result.Section2Content!, onDisk);
        Assert.EndsWith(result.Section2Content!, onDisk); // §2 runs to EOF
    }

    [Fact]
    public void Read_missing_constitution_surfaces_and_proceeds_without_throwing()
    {
        string temp = NewTempDir();
        try
        {
            ConstitutionReadResult result = ConstitutionService.Read(temp); // no .doti/memory/constitution.md
            Assert.False(result.Exists);
            Assert.Null(result.Section2Content);
            Assert.NotNull(result.AbsenceNote);
            Assert.Contains("/doti-constitution", result.AbsenceNote!);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    // ---- T010 / SC-003 : §2 extraction is byte-exact under both CRLF and LF

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ExtractSection2_is_verbatim_slice_to_eof(string nl)
    {
        string s2 = $"## §2 — Project declarations{nl}{nl}### Domain principles{nl}{nl}real content{nl}";
        string content = $"# Title{nl}{nl}## §1 — Inherited{nl}{nl}body{nl}{nl}{s2}";

        string? extracted = ConstitutionService.ExtractSection2(content);

        Assert.Equal(s2, extracted); // byte-identical to the on-disk §2 substring, line-ending-robust
    }

    [Fact]
    public void ExtractSection2_returns_null_when_no_anchor()
    {
        Assert.Null(ConstitutionService.ExtractSection2("# Title\n\nno section two here\n"));
    }

    // ---- T015 / FR-010, SC-002, SC-006, SC-010 : initialize-from-template, then preserve

    [Fact]
    public void Initialize_fills_title_when_absent_then_preserves_operator_edit()
    {
        string temp = NewTempDir();
        try
        {
            string template = "# {PROJECT_NAME} Constitution\n\n## §1 — Inherited\n\ncited\n\n## §2 — Project declarations\n\n[DOMAIN_PRINCIPLES]\n";

            ConstitutionInitResult first = ConstitutionInitializer.Initialize(temp, template, "Acme.Widget");
            Assert.Equal(ConstitutionInitOutcome.Initialized, first.Outcome);
            string written = File.ReadAllText(Path.Combine(temp, ".doti", "memory", "constitution.md"));
            Assert.Contains("# Acme.Widget Constitution", written);   // SC-010: title filled, no token left
            Assert.DoesNotContain("{PROJECT_NAME}", written);
            Assert.Contains("[DOMAIN_PRINCIPLES]", written);          // §2 placeholders intact for the operator

            // operator edits §2, then a re-install must PRESERVE it (SC-006: never resurrect the template)
            string edited = written.Replace("[DOMAIN_PRINCIPLES]", "Ship guarded scaffolds.");
            File.WriteAllText(Path.Combine(temp, ".doti", "memory", "constitution.md"), edited);

            ConstitutionInitResult second = ConstitutionInitializer.Initialize(temp, template, "Acme.Widget");
            Assert.Equal(ConstitutionInitOutcome.Preserved, second.Outcome);
            Assert.Equal(edited, File.ReadAllText(Path.Combine(temp, ".doti", "memory", "constitution.md")));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    // ---- T017 / FR-009, SC-002 : a fresh install ships the template + skill and yields a constitution

    [Fact]
    public void Install_ships_template_and_skill_and_yields_a_constitution()
    {
        string repo = FindRepoRoot();
        string temp = NewTempDir();
        try
        {
            DotiInstaller.Install(repo, temp, DotiAgentTarget.All, "Acme.Widget", force: true, projectNameOverride: "Acme.Widget");

            Assert.True(File.Exists(Path.Combine(temp, ".doti", "core", "templates", "constitution-template.md"))); // template shipped
            Assert.True(File.Exists(Path.Combine(temp, ".doti", "memory", "constitution.md")));                     // a constitution exists
            Assert.True(File.Exists(Path.Combine(temp, ".claude", "skills", "doti-constitution", "SKILL.md")));     // skill rendered
            // the constitution the stages will read is never empty + has no leftover title token
            string constitution = File.ReadAllText(Path.Combine(temp, ".doti", "memory", "constitution.md"));
            Assert.Contains("## §2", constitution);
            Assert.DoesNotContain("{PROJECT_NAME}", constitution);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    // ---- 010 / FR-001, SC-001 : the constitution is single-sourced (no .doti/core/memory twin)

    [Fact]
    public void Constitution_is_single_sourced_with_no_core_memory_twin()
    {
        string repo = FindRepoRoot();

        // the vestigial source-layer twin must NOT exist — the constitution lives only at .doti/memory/constitution.md
        Assert.False(
            File.Exists(Path.Combine(repo, ".doti", "core", "memory", "constitution.md")),
            "the redundant .doti/core/memory/constitution.md twin must not exist; the constitution is single-sourced at .doti/memory/constitution.md");

        string active = Path.Combine(repo, ".doti", "memory", "constitution.md");
        Assert.True(File.Exists(active));
        string text = File.ReadAllText(active);
        Assert.Contains("## §1", text);
        Assert.Contains("## §2", text);
        foreach (string placeholder in Section2Placeholders)
        {
            Assert.DoesNotContain(placeholder, text);
        }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "doti-constitution-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "scaffold-dotnet.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir)!;
        }

        throw new InvalidOperationException("Could not locate the repo root (scaffold-dotnet.slnx).");
    }
}
