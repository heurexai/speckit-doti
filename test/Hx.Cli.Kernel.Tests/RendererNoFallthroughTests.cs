using System.CommandLine;
using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cli.Kernel.Tests;

/// <summary>
/// 007 T013 (FR-018, SC-006): no command surface falls through to System.CommandLine's default help/version/
/// parse-error output. Every help form, <c>--version</c>, unknown-subcommand, unknown-mid-path token, unknown
/// option, and missing-required-arg drives through <see cref="CliApp.Harden"/> and renders the branded
/// <c>CliResult</c> envelope / branded help — never SCL's raw text. Driving real command shapes (a group, a leaf,
/// a required-option command) verifies the interception structurally, not by enumerating known commands.
/// </summary>
public sealed class RendererNoFallthroughTests
{
    private static readonly CliMeta Meta = new("hx-test", "1.2.3");

    private static RootCommand BuildRoot()
    {
        var root = new RootCommand("test tool");
        var group = new Command("doti", "Grouped commands.");
        var leaf = new Command("install", "Leaf command.");
        leaf.SetAction(_ => 0);
        group.Subcommands.Add(leaf);
        var newCmd = new Command("new", "Create something.");
        newCmd.Options.Add(new Option<string>("--name") { Required = true });
        newCmd.SetAction(_ => 0);
        root.Subcommands.Add(group);
        root.Subcommands.Add(newCmd);
        return root;
    }

    // Help renders to Console.Out (TextWriter); CliResult envelopes render as bytes to the output Stream.
    private static (string Console, JsonElement Envelope) Run(params string[] args)
    {
        using var stream = new MemoryStream();
        TextWriter origOut = Console.Out;
        using var sw = new StringWriter();
        try
        {
            Console.SetOut(sw);
            CliApp.Harden(BuildRoot(), Meta, args, "speckit-doti", "tagline", stream);
        }
        finally
        {
            Console.SetOut(origOut);
        }

        string json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        JsonElement envelope = string.IsNullOrWhiteSpace(json)
            ? default
            : JsonDocument.Parse(json).RootElement.Clone();
        return (sw.ToString(), envelope);
    }

    [Theory]
    [InlineData("frobnicate", "--json")]                 // unknown subcommand
    [InlineData("doti", "frobnicate", "--json")]         // unknown mid-path token
    [InlineData("doti", "install", "--bogus", "--json")] // unknown option
    [InlineData("new", "--json")]                        // missing required --name
    public void ParseErrors_RenderBrandedUsageEnvelope_NotSclDefault(params string[] args)
    {
        JsonElement env = Run(args).Envelope;
        Assert.Equal("usage", env.GetProperty("command").GetString());
        Assert.Equal((int)ExitClass.Usage, env.GetProperty("exitCode").GetInt32());
        Assert.False(env.GetProperty("ok").GetBoolean());
        Assert.NotEmpty(env.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void RootVersion_RendersBrandedVersionEnvelope_NotSclDefaultVersionLine()
    {
        JsonElement env = Run("--version", "--json").Envelope;
        Assert.Equal("version", env.GetProperty("command").GetString());
        Assert.True(env.GetProperty("ok").GetBoolean());
        Assert.Equal("1.2.3", env.GetProperty("data").GetProperty("version").GetString());
    }

    [Fact]
    public void Help_AtEveryLevel_RendersBrandedHelp_NotSclDefault()
    {
        Assert.Contains("speckit-doti", Run("--help").Console);                              // root: banner
        Assert.Contains("hx-test doti - Grouped commands.", Run("doti", "--help").Console);  // group
        Assert.Contains("hx-test doti install - Leaf command.", Run("doti", "install", "--help").Console); // leaf
    }
}
