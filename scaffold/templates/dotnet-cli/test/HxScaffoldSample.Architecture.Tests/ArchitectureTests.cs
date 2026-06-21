using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using HxScaffoldSample;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

namespace HxScaffoldSample.Architecture.Tests;

/// <summary>
/// The default architecture rule families (intent in rules/architecture.json). Nine families:
/// six structural (each with a negative fixture, except the positive-only cycle family), a
/// security capability-confinement family, an agent-first output-confinement family, and a CLI
/// surface-confinement (thin-adapter) family — the non-structural ones guarded against vacuous
/// passing. These run in the
/// generated repo's default `dotnet test`. Code-level insecure patterns are caught separately by
/// the .NET analyzer security rules enabled in Directory.Build.props.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly ArchUnitNET.Domain.Architecture Arch =
        new ArchLoader()
            .LoadAssemblies(
                typeof(GreetingService).Assembly,
                typeof(HxScaffoldSample.Cli.Program).Assembly,
                typeof(System.CommandLine.RootCommand).Assembly,
                // Capability assemblies loaded so the security (capability-confinement) rules are
                // enforced against real loaded types, not vacuously true.
                typeof(System.Net.Http.HttpClient).Assembly,
                typeof(System.Diagnostics.Process).Assembly,
                typeof(System.Reflection.Emit.DynamicMethod).Assembly,
                // System.Console so the agent-first output-confinement rule is enforced, not vacuous.
                typeof(System.Console).Assembly)
            .Build();

    private const string LibraryNs = "^HxScaffoldSample$";
    private const string CliNs = @"^HxScaffoldSample\.Cli$";
    private const string CommandLineNs = "^System\\.CommandLine";

    // 1. Namespace dependency — the library must not depend on the CLI.
    [Fact]
    public void Library_must_not_depend_on_the_cli()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(LibraryNs)
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(CliNs));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_namespace_dependency_is_detected()
    {
        // The CLI depends on the library by design, so this inverted rule MUST report violations.
        IArchRule wrong = Types().That().ResideInNamespaceMatching(CliNs)
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(LibraryNs));
        Assert.False(wrong.HasNoViolations(Arch));
    }

    // 2. Class dependency — the library must not depend on the System.CommandLine CLI framework.
    [Fact]
    public void Library_must_not_depend_on_system_commandline()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(LibraryNs)
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(CommandLineNs));
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_class_dependency_is_detected()
    {
        // The CLI does depend on System.CommandLine, so this inverted rule MUST report violations.
        IArchRule wrong = Types().That().ResideInNamespaceMatching(CliNs)
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(CommandLineNs));
        Assert.False(wrong.HasNoViolations(Arch));
    }

    // 3. Inheritance naming — implementations of IGreetingService are named *Service.
    [Fact]
    public void Greeting_service_implementations_are_named_service()
    {
        IArchRule rule = Classes().That().ImplementInterface(typeof(IGreetingService))
            .Should().HaveNameEndingWith("Service");
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_inheritance_naming_is_detected()
    {
        IArchRule wrong = Classes().That().ImplementInterface(typeof(IGreetingService))
            .Should().HaveNameEndingWith("Controller");
        Assert.False(wrong.HasNoViolations(Arch));
    }

    // 4. Class namespace containment — *Service classes live in the library.
    [Fact]
    public void Service_classes_live_in_the_library()
    {
        IArchRule rule = Classes().That().HaveNameEndingWith("Service")
            .Should().ResideInNamespaceMatching(LibraryNs);
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_namespace_containment_is_detected()
    {
        IArchRule wrong = Classes().That().HaveNameEndingWith("Service")
            .Should().ResideInNamespaceMatching(CliNs);
        Assert.False(wrong.HasNoViolations(Arch));
    }

    // 5. Attribute access — [SerializableContract] DTOs live in the library, not the CLI.
    [Fact]
    public void Serializable_contracts_live_in_the_library()
    {
        IArchRule rule = Classes().That().HaveAnyAttributes(typeof(SerializableContractAttribute))
            .Should().ResideInNamespaceMatching(LibraryNs);
        Assert.True(rule.HasNoViolations(Arch));
    }

    [Fact]
    public void Negative_attribute_access_is_detected()
    {
        IArchRule wrong = Classes().That().HaveAnyAttributes(typeof(SerializableContractAttribute))
            .Should().ResideInNamespaceMatching(CliNs);
        Assert.False(wrong.HasNoViolations(Arch));
    }

    // 6. Cycle — the sample namespaces are free of dependency cycles.
    [Fact]
    public void Namespaces_are_free_of_cycles()
    {
        IArchRule rule = Slices().Matching("HxScaffoldSample.(*)").Should().BeFreeOfCycles();
        Assert.True(rule.HasNoViolations(Arch));
    }

    // 7. Security architecture (capability confinement / least privilege) — the domain library
    //    must not reach for dangerous capabilities: process execution, networking, or dynamic
    //    code generation. Those belong in the CLI or dedicated adapters, keeping the domain's
    //    attack surface small. (Insecure code patterns are caught by the analyzer security rules.)
    [Fact]
    public void Domain_library_does_not_depend_on_dangerous_capabilities()
    {
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

    // 8. Output confinement (agent-first) — every command renders through the Agent host (the CliResult
    //    envelope, JSON-first); only the Agent type writes to the console, so output stays one
    //    machine-consumable chokepoint instead of scattered Console.Write calls.
    [Fact]
    public void Only_the_agent_host_writes_to_the_console()
    {
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

    // 9. CLI surface confinement (Channel Independence / thin adapter) — the CLI carries no business-logic
    //    types (*Service/*Repository/*Validator/*Calculator/*Engine/*Manager/*Scanner/*Provider); those
    //    live in the domain library, so the CLI stays a thin channel adapter the core never depends on.
    [Fact]
    public void Cli_carries_no_business_logic_types()
    {
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
