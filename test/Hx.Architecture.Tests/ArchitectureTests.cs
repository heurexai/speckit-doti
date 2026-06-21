using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Hx.Impact.Cli;
using Hx.Runner.Cli;
using Hx.Runner.Core.Tools;
using Hx.Scaffold.Cli;
using Hx.Tooling.Contracts;
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
            .AndShould().NotHaveNameEndingWith("Provider");
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
}
