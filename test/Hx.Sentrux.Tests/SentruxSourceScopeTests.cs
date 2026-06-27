using Hx.Sentrux.Core;
using Xunit;

namespace Hx.Sentrux.Tests;

/// <summary>T022 (FR-029/SC-016): Sentrux's source scope is CODE only — prose (docs, *.md, .doti / .agents / .claude)
/// is out of scope (the floor), .cs/.csproj are in, and an explicitly-configured-as-code path overrides the floor.</summary>
public sealed class SentruxSourceScopeTests
{
    private static readonly SentruxPolicy Policy = SentruxPolicy.Default();

    [Theory]
    [InlineData("tools/Hx.Cycle.Core/CycleService.cs", true)]
    [InlineData("tools/Hx.Cycle.Core/Hx.Cycle.Core.csproj", true)]
    [InlineData("README.md", false)]
    [InlineData("docs/specs/008.md", false)]
    [InlineData(".doti/core/templates/commands/doti-analyze.md", false)]
    [InlineData(".doti/core/skills.json", false)]
    [InlineData(".claude/skills/05-doti-analyze/SKILL.md", false)]
    [InlineData(".agents/skills/05-doti-analyze/SKILL.md", false)]
    public void IsInScope_excludes_prose_and_includes_code(string path, bool expected) =>
        Assert.Equal(expected, SentruxSourceScope.IsInScope(path, Policy));

    [Fact]
    public void ConfiguredAsCode_overrides_the_prose_floor()
    {
        SentruxPolicy withOverride = Policy with { ConfiguredAsCode = ["docs/generated-code"] };

        Assert.True(SentruxSourceScope.IsInScope("docs/generated-code/Thing.md", withOverride));
        Assert.False(SentruxSourceScope.IsInScope("docs/other/Thing.md", withOverride));
    }

    [Fact]
    public void Default_code_extensions_match_the_csharp_grammar()
    {
        Assert.Equal([".cs", ".csproj"], Policy.EffectiveCodeExtensions);
    }
}
