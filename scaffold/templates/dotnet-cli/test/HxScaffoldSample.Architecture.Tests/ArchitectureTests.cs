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
public sealed partial class ArchitectureTests
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

}
