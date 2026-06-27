using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Hx.Impact.Cli;
using Hx.Runner.Cli;
using Hx.Runner.Core.Tools;
using Hx.Scaffold.Cli;
using Hx.Tooling.Contracts;
using System.Text.Json;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Hx.Architecture.Tests;

/// <summary>
/// The scaffold dog-foods its own "thin CLI, fat core" rule (the Channel Independence principle): the
/// <c>Hx.*.Cli</c> channels carry no business logic, and command types delegate to a <c>*.Core</c> library.
/// These are the same families the generated template ships, applied to the toolkit's own CLIs so the
/// scaffold cannot violate the rule it enforces on its users.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly ArchUnitNET.Domain.Architecture Arch =
        new ArchLoader()
            .LoadAssemblies(
                typeof(RunnerCommands).Assembly,
                typeof(ScaffoldCommands).Assembly,
                typeof(ImpactCommands).Assembly,
                typeof(ToolStore).Assembly,                                    // Hx.Runner.Core
                typeof(Hx.Scaffold.Core.ToolVendor).Assembly,                  // Hx.Scaffold.Core
                typeof(Hx.Impact.Core.Planning.AffectedTestPlanner).Assembly,  // Hx.Impact.Core
                typeof(Hx.Cycle.Core.CycleStateStore).Assembly,                // Hx.Cycle.Core
                typeof(Hx.Gate.Core.GateRunner).Assembly,                      // Hx.Gate.Core
                typeof(Hx.Doti.Core.DotiInstaller).Assembly,                   // 009 L3: Hx.Doti.Core (constitution)
                typeof(Hx.Embedding.ModelLocator).Assembly,                    // 008 H-3: Hx.Embedding.Core
                typeof(Hx.Semantic.DriftCandidateRunner).Assembly,             // 008 H-3: Hx.Semantic.Core
                typeof(Hx.Semantic.Cli.SemanticCommandFactory).Assembly,       // 008 H-3: Hx.Semantic.Cli
                typeof(CliResult).Assembly,                                    // Hx.Tooling.Contracts
                typeof(System.CommandLine.RootCommand).Assembly)
            .Build();

    // CLI channel namespaces end in ".Cli" (Hx.Runner.Cli, Hx.Scaffold.Cli, Hx.Impact.Cli).
    // Hx.Cli.Kernel ends in ".Kernel", so it is correctly treated as core, not a channel.
    private const string CliNs = @"\.Cli$";
    private const string CoreNs = @"^Hx\..*\.Core";

    // cliSurfaceConfinement — the channel carries no business-logic types; those live in *.Core.
    [Fact]
    public void Cli_namespaces_carry_no_business_logic_types()
    {
        IArchRule rule = Classes().That().ResideInNamespaceMatching(CliNs)
            .Should().NotHaveNameEndingWith("Service")
            .AndShould().NotHaveNameEndingWith("Repository")
            .AndShould().NotHaveNameEndingWith("Validator")
            .AndShould().NotHaveNameEndingWith("Scanner")
            .AndShould().NotHaveNameEndingWith("Runner")
            .AndShould().NotHaveNameEndingWith("Resolver")
            .AndShould().NotHaveNameEndingWith("Engine")
            .AndShould().NotHaveNameEndingWith("Provider")
            // 008 BL-5: the 008 substrate adds *Planner/*Classifier/*Projector/*Detector logic types; confine them to
            // *.Core so a future CLI can never inline the recovery planner, restamp classifier, review projector, or
            // release-train detector.
            .AndShould().NotHaveNameEndingWith("Planner")
            .AndShould().NotHaveNameEndingWith("Classifier")
            .AndShould().NotHaveNameEndingWith("Projector")
            .AndShould().NotHaveNameEndingWith("Detector");
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_cli_confinement_is_detected()
    {
        // *Service / *Runner / *Resolver types exist in core, so asserting they live in the CLI MUST report
        // violations — proving the suffix matcher is real, not vacuously satisfied by an empty set.
        IArchRule wrong = Classes().That().HaveNameEndingWith("Runner").Or().HaveNameEndingWith("Resolver")
            .Should().ResideInNamespaceMatching(CliNs);
        Assert.False(wrong.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_cli_confinement_covers_the_008_suffixes()
    {
        // 008 BL-5 guard: *Planner/*Classifier/*Projector/*Detector types live in core (CycleRecoveryPlanner,
        // RestampSafetyClassifier, ReviewContextProjector, ReleaseTrainDriftDetector, AffectedTestPlanner) — asserting
        // they reside in a CLI namespace MUST report violations, proving the new suffix matchers are real.
        IArchRule wrong = Classes().That()
            .HaveNameEndingWith("Planner").Or().HaveNameEndingWith("Classifier")
            .Or().HaveNameEndingWith("Projector").Or().HaveNameEndingWith("Detector")
            .Should().ResideInNamespaceMatching(CliNs);
        Assert.False(wrong.HasNoViolations(Arch));
    }

    // cliDelegation — every command-dispatch type routes to a core library (the CLI is a channel, not the logic).
    [Fact]
    public void Cli_command_types_delegate_to_a_core_library()
    {
        IArchRule rule = Classes().That().ResideInNamespaceMatching(CliNs).And().HaveNameEndingWith("Commands")
            .Should().DependOnAny(Types().That().ResideInNamespaceMatching(CoreNs));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Cli_has_command_types_so_delegation_is_not_vacuous()
    {
        // The delegation rule must have real subjects: at least one *Commands type in a .Cli namespace.
        Assert.Contains(Arch.Types, t =>
            t.FullName.Contains(".Cli.", System.StringComparison.Ordinal)
            && t.FullName.EndsWith("Commands", System.StringComparison.Ordinal));
    }

    // 008 semantic-stack namespaces. Hx.Embedding.Core lives under Hx.Embedding, Hx.Semantic.Core/Cli under Hx.Semantic.
    private const string EmbeddingNs = @"^Hx\.Embedding";
    private const string SemanticStackNs = @"^Hx\.(Embedding|Semantic)";

    // FR-040 / FR-021 (H-7 broadened): Hx.Embedding.Core is a self-contained, OFFLINE ML port — it depends on no
    // Hx.* assembly except Hx.Tooling.Contracts, and on nothing under System.Net.*. Either dependency would couple
    // the advisory finder to the workflow substrate or the network, both forbidden for a deterministic local port.
    [Fact]
    public void Embedding_core_depends_on_no_hx_assembly_except_contracts()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(EmbeddingNs)
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^Hx\.")
                .And().DoNotResideInNamespaceMatching(EmbeddingNs)
                .And().DoNotResideInNamespaceMatching(@"^Hx\.Tooling\.Contracts"));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Semantic_stack_has_no_network_dependency()
    {
        // H-7 (FR-021 broadened): the entire semantic stack is offline/local — no System.Net.* (no HTTP, no sockets).
        IArchRule rule = Types().That().ResideInNamespaceMatching(SemanticStackNs)
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^System\.Net"));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Embedding_namespace_has_real_subjects()
    {
        // Guard: the FR-040/FR-021 rules above must load real Hx.Embedding types, not pass over an empty set.
        Assert.Contains(Arch.Types, t => t.FullName.StartsWith("Hx.Embedding.", System.StringComparison.Ordinal));
    }

    // FR-020 / SC-009: the deterministic Gate/Cycle Cores MUST NOT depend on the advisory semantic stack — the
    // semantic finder is never a gate or proof input. Compile-checked: a stray reference fails the build's test gate.
    [Fact]
    public void Gate_and_cycle_cores_do_not_depend_on_the_semantic_stack()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Hx\.(Gate|Cycle)\.Core")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(SemanticStackNs));
        Assert.True(rule.HasNoViolations(Arch));
    }

    // 009 L3: the §2 constitution composition lives in the RUNNER (RunnerCommands.Doti.ReviewContext), NEVER in
    // Hx.Cycle.Core — so review-context carries the constitution without a Cycle→Doti core edge (Hx.Doti.Core depends
    // ON Hx.Cycle.Core, not the reverse). Compile-checked: a stray Cycle→Doti reference fails the build's test gate.
    [Fact]
    public void Cycle_core_does_not_depend_on_doti_core()
    {
        Assert.Contains(Arch.Types, t => t.Name.Equals("DotiInstaller", System.StringComparison.Ordinal)); // real target
        Assert.Contains(Arch.Types, t => t.Name.Equals("CycleStateStore", System.StringComparison.Ordinal)); // real subject
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Hx\.Cycle\.Core")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^Hx\.Doti\.Core"));
        Assert.True(rule.HasNoViolations(Arch));
    }

    // M-8: the gate-proof hashers project canonical proof inputs only — ChangeSetContext is a REVIEW context (arch-review
    // lens applicability, drift candidates), never a proof input. A *ProofHasher depending on it would let the advisory
    // change context leak into a deterministic proof hash. Compile-checked.
    [Fact]
    public void Gate_proof_hashers_do_not_depend_on_change_set_context()
    {
        IArchRule rule = Classes().That().HaveNameEndingWith("ProofHasher")
            .Should().NotDependOnAny(Types().That().HaveFullNameContaining("ChangeSetContext"));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Proof_hashers_and_change_set_context_are_both_loaded()
    {
        // Guard: the M-8 rule must have a real subject (a *ProofHasher) and a real forbidden target (ChangeSetContext).
        Assert.Contains(Arch.Types, t => t.Name.EndsWith("ProofHasher", System.StringComparison.Ordinal));
        Assert.Contains(Arch.Types, t => t.Name.Equals("ChangeSetContext", System.StringComparison.Ordinal));
    }

    // 012 M1 (the load-bearing boundary): the visibility records — GateTrace, ChangeSummary, AffectedTestInventory —
    // are REVIEW/TELEMETRY context carried on the GateRunResult/GateProof envelope, NEVER a proof-hash input (008
    // FR-020/SC-009). A *ProofHasher depending on any of them would let advisory change-context leak into a
    // deterministic proof. Compile-checked, extending the M-8 pattern to the new records.
    [Fact]
    public void Gate_proof_hashers_do_not_depend_on_the_visibility_trace_records()
    {
        IArchRule rule = Classes().That().HaveNameEndingWith("ProofHasher")
            .Should().NotDependOnAny(Types().That()
                .HaveFullNameContaining("GateTrace")
                .Or().HaveFullNameContaining("ChangeSummary")
                .Or().HaveFullNameContaining("AffectedTestInventory"));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Visibility_trace_records_are_loaded()
    {
        // Guard: the M1 rule must have real forbidden targets (the three 012 visibility records).
        Assert.Contains(Arch.Types, t => t.Name.Equals("GateTrace", System.StringComparison.Ordinal));
        Assert.Contains(Arch.Types, t => t.Name.Equals("ChangeSummary", System.StringComparison.Ordinal));
        Assert.Contains(Arch.Types, t => t.Name.Equals("AffectedTestInventory", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Architecture_guidance_matches_declared_families()
    {
        string root = FindRepositoryRoot();
        string[] familyIds = LoadArchitectureFamilyIds(root);
        Assert.Equal(["cliSurfaceConfinement", "cliDelegation"], familyIds);

        string[] guidanceFiles =
        [
            Path.Combine(root, "README.md"),
            Path.Combine(root, ".doti", "core", "skills.json"),
            Path.Combine(root, ".doti", "core", "templates", "agent-context-template.md"),
            Path.Combine(root, ".doti", "core", "templates", "commands", "doti-arch-review.md"),
        ];

        foreach (string path in guidanceFiles)
        {
            string text = File.ReadAllText(path);
            foreach (string familyId in familyIds)
            {
                Assert.Contains(familyId, text, System.StringComparison.Ordinal);
            }

            Assert.DoesNotContain("nine ArchUnitNET", text, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nine rule", text, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nine families", text, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "scaffold-dotnet.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from " + AppContext.BaseDirectory);
    }

    private static string[] LoadArchitectureFamilyIds(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, "rules", "architecture.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("families")
            .EnumerateArray()
            .Select(family => family.GetProperty("id").GetString() ?? string.Empty)
            .Where(id => id.Length > 0)
            .ToArray();
    }
}
