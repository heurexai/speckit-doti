using System.CommandLine;
using System.Text;
using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cli.Kernel.Tests;

public sealed class CliKernelTests
{
    private static readonly CliMeta Meta = new("hx-test", "1.2.3");

    private static (string Text, byte[] Bytes) WriteJson(CliResult result)
    {
        using var stream = new MemoryStream();
        CliWriter.WriteJson(stream, result);
        byte[] bytes = stream.ToArray();
        return (Encoding.UTF8.GetString(bytes), bytes);
    }

    [Fact]
    public void Ok_Envelope_HasCoreRings_AndIsLfNormalized()
    {
        CliResult result = CliResults.Ok(Meta, "greet", "Hello.", new { name = "world" });
        (string text, _) = WriteJson(result);

        // LF-normalized: exactly one trailing \n, no \r anywhere, compact (no internal newlines).
        Assert.EndsWith("\n", text);
        Assert.DoesNotContain('\r', text);
        Assert.Single(text.TrimEnd('\n').Split('\n'));

        using JsonDocument doc = JsonDocument.Parse(text);
        JsonElement root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("hx-test", root.GetProperty("tool").GetString());
        Assert.Equal("greet", root.GetProperty("command").GetString());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("success", root.GetProperty("outcome").GetString());
        Assert.Equal("world", root.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public void Fail_Envelope_NotOk_WithExitClassAndRegistryDiagnostic()
    {
        CliResult result = CliResults.Fail(Meta, "plan", ExitClass.Validation,
            [Diag.Of(ErrorCodes.Validation_Failed, target: "spec.md")]);
        (string text, _) = WriteJson(result);

        using JsonDocument doc = JsonDocument.Parse(text);
        JsonElement root = doc.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal((int)ExitClass.Validation, root.GetProperty("exitCode").GetInt32());
        JsonElement[] errors = [.. root.GetProperty("errors").EnumerateArray()];
        JsonElement err = Assert.Single(errors);
        Assert.Equal("VAL0001", err.GetProperty("code").GetString());
        Assert.Equal("spec.md", err.GetProperty("target").GetString());
        Assert.Equal("error", err.GetProperty("severity").GetString());
    }

    [Fact]
    public void WriteJson_IsByteIdentical_AcrossCalls()
    {
        CliResult result = CliResults.Ok(Meta, "greet", "Hi.", new { n = 1 });
        (_, byte[] a) = WriteJson(result);
        (_, byte[] b) = WriteJson(result);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Diag_Of_UnregisteredCode_Throws() =>
        Assert.Throws<ArgumentException>(() => Diag.Of("ZZZ9999"));

    [Fact]
    public void Diag_Of_RegisteredCode_CarriesRegistryMetadata()
    {
        Diagnostic d = Diag.Of(ErrorCodes.Internal_Unhandled);
        Assert.Equal("INT0001", d.Code);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal("An unhandled internal error occurred.", d.Message);
        Assert.False(string.IsNullOrEmpty(d.Hint));
    }

    [Fact]
    public void Describe_WalksTheCommandTree_AndIncludesCatalogs()
    {
        var root = new RootCommand("test tool");
        var greet = new Command("greet", "Print a greeting.");
        greet.Options.Add(new Option<string>("--name") { Description = "Who." });
        root.Subcommands.Add(greet);

        CliDescribe describe = DescribeWalker.Describe(Meta, root, ErrorCodes.All);

        Assert.Equal("hx-test", describe.Tool);
        CliDescribeCommand g = Assert.Single([.. describe.Root.Subcommands.Where(c => c.Name == "greet")]);
        Assert.Equal("Print a greeting.", g.Summary);
        Assert.Contains(g.Options, o => o.Name == "--name");
        Assert.Contains("Internal", describe.ExitClasses);
        Assert.Contains(describe.ErrorCodeCatalog, e => e.Code == "INT0001");
    }

    [Fact]
    public void PlainHelp_UsesTheSameModel_ForNestedCommands()
    {
        var root = new RootCommand("test tool");
        var group = new Command("group", "Grouped commands.");
        var leaf = new Command("leaf", "Leaf command.");
        leaf.Options.Add(new Option<bool>("--json") { Description = "JSON output." });
        group.Subcommands.Add(leaf);
        root.Subcommands.Add(group);

        string help = CliRenderer.RenderPlainHelp(root, leaf, ["group", "leaf"], Meta, "speckit-doti", "tagline");

        Assert.Contains("hx-test group leaf - Leaf command.", help);
        Assert.Contains("Usage:", help);
        Assert.Contains("--json", help);
        Assert.Contains("--help-mode <auto|rich|plain>", help);
        Assert.DoesNotContain("\x1b[", help, StringComparison.Ordinal);
    }

    [Fact]
    public void CliApp_Invoke_InterceptsSubcommandHelp_AndCanForcePlain()
    {
        var root = new RootCommand("test tool");
        var group = new Command("group", "Grouped commands.");
        var leaf = new Command("leaf", "Leaf command.");
        group.Subcommands.Add(leaf);
        root.Subcommands.Add(group);

        TextWriter original = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            int exit = CliApp.Invoke(root, Meta,
                ["group", "leaf", "--help-mode", "plain", "--help"], "speckit-doti", "tagline");

            Assert.Equal(0, exit);
            string help = writer.ToString();
            Assert.Contains("hx-test group leaf - Leaf command.", help);
            Assert.Contains("--plain-help", help);
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
