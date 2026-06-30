using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T002 (FR-009/SC-006, D5/D9): fail-closed schema + value validation — each error names the offending
/// field; an invalid config never validates (so the CLI never generates).</summary>
public sealed class SetupConfigSchemaTests
{
    private static readonly string Bell = ((char)7).ToString();

    [Fact]
    public void SchemaVersion_other_than_one_fails_naming_the_field()
    {
        // SC-006: schemaVersion: 2 → fail closed.
        SetupValidationResult result = SetupConfigSchema.ValidateRaw("{\"schemaVersion\":2}", out SetupConfig? config);
        Assert.False(result.Ok);
        Assert.Null(config);
        Assert.Contains(result.Errors, e => e.Field == "schemaVersion");
    }

    [Fact]
    public void Unknown_agent_fails_naming_agents()
    {
        // SC-006: agents: ["gemini"] → fail closed naming the field.
        SetupValidationResult result = SetupConfigSchema.ValidateRaw("{\"agents\":[\"gemini\"]}", out _);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == "agents" && e.Message.Contains("gemini"));
    }

    [Fact]
    public void Unknown_top_level_field_is_rejected()
    {
        SetupValidationResult result = SetupConfigSchema.ValidateRaw("{\"sentrux\":{\"max_cycles\":1}}", out _);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == "sentrux");
    }

    [Fact]
    public void Unknown_nested_field_is_rejected_with_dotted_path()
    {
        SetupValidationResult result = SetupConfigSchema.ValidateRaw("{\"identity\":{\"copyright\":\"x\"}}", out _);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == "identity.copyright");
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("v1.2.3")]
    public void Non_semver_next_version_fails(string version)
    {
        var config = new SetupConfig { Versioning = new SetupVersioningConfig { NextVersion = version } };
        SetupValidationResult result = SetupConfigSchema.Validate(config);
        Assert.Contains(result.Errors, e => e.Field == "versioning.nextVersion");
    }

    [Fact]
    public void Valid_three_part_semver_passes()
    {
        var config = new SetupConfig { Versioning = new SetupVersioningConfig { NextVersion = "0.1.0" } };
        Assert.True(SetupConfigSchema.Validate(config).Ok);
    }

    [Fact]
    public void Xml_metacharacter_in_identity_free_text_is_rejected()
    {
        // D9: an MSBuild-injection vector — a raw '<' in a free-text identity value.
        var config = new SetupConfig { Identity = new SetupIdentityConfig { Description = "Bad <Inject>" } };
        SetupValidationResult result = SetupConfigSchema.Validate(config);
        Assert.Contains(result.Errors, e => e.Field == "identity.description");
    }

    [Fact]
    public void Ampersand_in_description_is_rejected_at_validation()
    {
        // The CLI rejects a raw '&' up front; the writer additionally XML-encodes a value that did slip through.
        var config = new SetupConfig { Identity = new SetupIdentityConfig { Description = "A & B" } };
        Assert.Contains(SetupConfigSchema.Validate(config).Errors, e => e.Field == "identity.description");
    }

    [Fact]
    public void Control_char_is_rejected()
    {
        var config = new SetupConfig { Identity = new SetupIdentityConfig { Company = "Bad" + Bell + "Co" } };
        Assert.Contains(SetupConfigSchema.Validate(config).Errors, e => e.Field == "identity.company");
    }

    [Theory]
    [InlineData("MIT")]
    [InlineData("Apache-2.0")]
    [InlineData("(MIT OR Apache-2.0)")]
    public void Valid_spdx_passes(string license)
    {
        var config = new SetupConfig { Identity = new SetupIdentityConfig { License = license } };
        Assert.True(SetupConfigSchema.Validate(config).Ok);
    }

    [Fact]
    public void Invalid_spdx_charset_is_rejected()
    {
        var config = new SetupConfig { Identity = new SetupIdentityConfig { License = "MIT;rm -rf" } };
        Assert.Contains(SetupConfigSchema.Validate(config).Errors, e => e.Field == "identity.license");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("/abs/path")]
    [InlineData("C:/abs")]
    [InlineData("a/../../b")]
    public void Output_path_traversal_is_rejected(string output)
    {
        var config = new SetupConfig { Identity = new SetupIdentityConfig { Output = output } };
        Assert.Contains(SetupConfigSchema.Validate(config).Errors, e => e.Field == "identity.output");
    }

    [Fact]
    public void Contained_relative_output_is_accepted()
    {
        var config = new SetupConfig { Identity = new SetupIdentityConfig { Output = "out/sub" } };
        Assert.True(SetupConfigSchema.Validate(config).Ok);
    }

    [Fact]
    public void Malformed_json_fails_closed()
    {
        SetupValidationResult result = SetupConfigSchema.ValidateRaw("{ not json ", out SetupConfig? config);
        Assert.False(result.Ok);
        Assert.Null(config);
    }

    [Fact]
    public void Constitution_section2_allows_markdown_metachars_but_rejects_control_chars()
    {
        // §2 prose legitimately carries '&'/'<' in Markdown; only control chars are rejected (the writer guards anchors).
        var ok = new SetupConfig { Constitution = new SetupConstitutionConfig { DomainPrinciples = "Ship A & B safely <fast>." } };
        Assert.True(SetupConfigSchema.Validate(ok).Ok);

        var bad = new SetupConfig { Constitution = new SetupConstitutionConfig { DomainPrinciples = "Bad" + Bell + "bell" } };
        Assert.Contains(SetupConfigSchema.Validate(bad).Errors, e => e.Field == "constitution.domainPrinciples");
    }
}
