using ArchUnitNET.Fluent;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace HxScaffoldSample.Architecture.Tests;

public sealed partial class ArchitectureTests
{
    [Fact]
    public void Domain_library_does_not_depend_on_dangerous_capabilities()
    {
        // Security architecture / least privilege: keep dangerous capabilities outside the domain library.
        var dangerous = Types().That().ResideInNamespaceMatching(@"^System\.Net\.")
            .Or().ResideInNamespaceMatching(@"^System\.Reflection\.Emit")
            .Or().HaveFullNameMatching(@"System\.Diagnostics\.Process");
        IArchRule rule = Types().That().ResideInNamespaceMatching(LibraryNs)
            .Should().NotDependOnAny(dangerous);
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Capability_namespaces_are_loaded_so_the_security_rule_is_not_vacuous()
    {
        // Guard against a silently-passing rule (the System.CommandLine family taught this lesson):
        // the forbidden capability types must actually be present in the analyzed architecture.
        Assert.Contains(Arch.Types, t => t.FullName.StartsWith("System.Net.Http", System.StringComparison.Ordinal));
        Assert.Contains(Arch.Types, t => t.FullName.StartsWith("System.Diagnostics.Process", System.StringComparison.Ordinal));
        Assert.Contains(Arch.Types, t => t.FullName.StartsWith("System.Reflection.Emit", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Only_the_agent_host_writes_to_the_console()
    {
        // Output confinement: all console writes flow through the Agent host.
        IArchRule rule = Types().That().ResideInNamespaceMatching(CliNs).And().DoNotHaveName("Agent")
            .Should().NotDependOnAny(Types().That().HaveFullNameMatching(@"^System\.Console$"));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Agent_host_is_the_console_chokepoint_so_output_confinement_is_not_vacuous()
    {
        // The capability must be present AND actually reached by the Agent host — otherwise the rule
        // above could pass simply because nothing depends on System.Console.
        Assert.Contains(Arch.Types, t => t.FullName == "System.Console");
        IArchRule rule = Classes().That().HaveName("Agent")
            .Should().DependOnAny(Types().That().HaveFullNameMatching(@"^System\.Console$"));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Cli_carries_no_business_logic_types()
    {
        // CLI surface confinement: keep the CLI as a thin adapter, not a business-logic home.
        IArchRule rule = Classes().That().ResideInNamespaceMatching(CliNs)
            .Should().NotHaveNameEndingWith("Service")
            .AndShould().NotHaveNameEndingWith("Repository")
            .AndShould().NotHaveNameEndingWith("Validator")
            .AndShould().NotHaveNameEndingWith("Calculator")
            .AndShould().NotHaveNameEndingWith("Engine")
            .AndShould().NotHaveNameEndingWith("Manager")
            .AndShould().NotHaveNameEndingWith("Scanner")
            .AndShould().NotHaveNameEndingWith("Provider");
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_cli_surface_confinement_is_detected()
    {
        // GreetingService (a *Service) lives in the library, so asserting *Service types reside in the CLI
        // MUST report violations — proving the confinement matcher is enforced, not vacuous.
        IArchRule wrong = Classes().That().HaveNameEndingWith("Service")
            .Should().ResideInNamespaceMatching(CliNs);
        Assert.False(wrong.HasNoViolations(Arch));
    }
}
