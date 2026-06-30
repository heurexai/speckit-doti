using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T014 (FR-005/D4): every wizard prompt maps 1:1 to a registered setup key, carries a "what it affects"
/// hint, and its group/audience agree with the key descriptor.</summary>
public sealed class SetupPromptDefinitionsTests
{
    [Fact]
    public void Every_prompt_maps_to_a_registered_key()
    {
        foreach (SetupPromptDefinition prompt in SetupPromptDefinitions.All)
        {
            SetupKey key = SetupKeys.ById_(prompt.Key); // throws if unregistered
            Assert.Equal(key.Group, prompt.Group);
            Assert.Equal(key.Audience, prompt.Audience);
            Assert.False(string.IsNullOrWhiteSpace(prompt.WhatItAffects));
            Assert.False(string.IsNullOrWhiteSpace(prompt.Question));
        }
    }

    [Fact]
    public void Prompt_keys_are_unique()
    {
        string[] keys = SetupPromptDefinitions.All.Select(p => p.Key).ToArray();
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void Publish_sub_questions_are_gated_on_publish_enabled()
    {
        SetupPromptDefinition owner = SetupPromptDefinitions.All.Single(p => p.Key == SetupKeys.PublishOwner);
        Assert.NotNull(owner.EnabledWhen);
        Assert.Equal(SetupKeys.PublishEnabled, owner.EnabledWhen!.Key);
        Assert.Equal("true", owner.EnabledWhen.EqualsValue);
    }
}
