using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cli.Kernel.Tests;

/// <summary>
/// Proves the error-code registry engine: the committed <c>ErrorCodes.g.cs</c> stays
/// consistent with <c>registry.json</c>, the shipped baseline holds, and a removed/mutated shipped code fails the
/// stability check (the negative test).
/// </summary>
public sealed class ErrorCodeRegistryTests
{
    private static string Errorcodes(string file)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found"), "errorcodes", file);
    }

    private static IReadOnlyList<ErrorCodeEntry> Registry() => ErrorCodeRegistry.Load(Errorcodes("registry.json"));
    private static IReadOnlyList<ShippedCode> Shipped() => ErrorCodeRegistry.LoadShipped(Errorcodes("shipped.json"));

    [Fact]
    public void Loaded_registry_matches_the_committed_generated_constants()
    {
        IReadOnlyList<ErrorCodeEntry> loaded = Registry();
        Assert.Equal(ErrorCodes.All.Count, loaded.Count);
        for (int i = 0; i < loaded.Count; i++)
        {
            Assert.Equal(ErrorCodes.All[i].Code, loaded[i].Code);
            Assert.Equal(ErrorCodes.All[i].Name, loaded[i].Name);
            Assert.Equal(ErrorCodes.All[i].ExitClass, loaded[i].ExitClass);
            Assert.Equal(ErrorCodes.All[i].Number, loaded[i].Number);
            Assert.Equal(ErrorCodes.All[i].Severity, loaded[i].Severity);
        }
    }

    [Fact]
    public void Numbers_are_per_category_sequential_and_codes_compose()
    {
        ErrorCodeEntry itg = Assert.Single([.. Registry().Where(e => e.Code == "ITG0001")]);
        Assert.Equal("integrity", itg.Category);
        Assert.Equal("ITG", itg.Prefix);
        Assert.Equal(1, itg.Number);
        Assert.Equal("integrity.verification-failed", itg.Name);
    }

    [Fact]
    public void CheckStability_passes_for_the_current_registry() =>
        Assert.Empty(ErrorCodeRegistry.CheckStability(Registry(), Shipped()));

    [Fact]
    public void CheckStability_fails_when_a_shipped_code_is_removed()
    {
        IReadOnlyList<ErrorCodeEntry> withoutItg = [.. Registry().Where(e => e.Code != "ITG0001")];
        IReadOnlyList<string> violations = ErrorCodeRegistry.CheckStability(withoutItg, Shipped());
        Assert.Contains(violations, v => v.Contains("ITG0001") && v.Contains("removed"));
    }

    [Fact]
    public void CheckStability_fails_when_a_shipped_code_name_changes()
    {
        // A shipped code keeping its number but renamed (e.g. a careless registry edit) breaks the consumer contract.
        IReadOnlyList<ShippedCode> tampered = [new ShippedCode("VAL0001", "validation.renamed", "Validation")];
        IReadOnlyList<string> violations = ErrorCodeRegistry.CheckStability(Registry(), tampered);
        Assert.Contains(violations, v => v.Contains("VAL0001") && v.Contains("name changed"));
    }

    [Fact]
    public void RenderConstants_emits_consts_and_manifest_with_lf_newlines()
    {
        string rendered = ErrorCodeRegistry.RenderConstants(Registry());
        Assert.Contains("public const string Internal_Unhandled = \"INT0001\";", rendered);
        Assert.Contains("public const string Integrity_VerificationFailed = \"ITG0001\";", rendered);
        Assert.Contains("ExitClass.Integrity", rendered);
        Assert.DoesNotContain('\r', rendered);
    }

    [Theory]
    [InlineData("internal.unhandled", "Internal_Unhandled")]
    [InlineData("usage.invalid-arguments", "Usage_InvalidArguments")]
    [InlineData("integrity.verification-failed", "Integrity_VerificationFailed")]
    public void ConstName_maps_dotted_hyphenated_names(string name, string expected) =>
        Assert.Equal(expected, ErrorCodeRegistry.ConstName(name));
}
