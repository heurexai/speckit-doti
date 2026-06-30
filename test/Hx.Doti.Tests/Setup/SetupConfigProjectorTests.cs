using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T004 (FR-002/FR-006, D3/D10): the projection orchestration dispatches each CUSTOM field to the
/// injected writer for its target, audience-scopes (new-only ignored on install), and provably no-ops when Setup is
/// null. Driven by FAKE writers (no IO) so the pure orchestration is tested in isolation.</summary>
public sealed class SetupConfigProjectorTests
{
    private sealed class FakeWriter(SetupTarget target) : ISetupTargetWriter
    {
        public SetupTarget Target { get; } = target;
        public List<ResolvedSetupField> Received { get; } = [];

        public IReadOnlyList<string> Write(string repositoryRoot, IReadOnlyList<ResolvedSetupField> fields)
        {
            Received.AddRange(fields);
            return fields.Select(f => $"wrote:{f.Key}").ToList();
        }
    }

    [Fact]
    public void Null_setup_calls_no_writer_and_writes_nothing()
    {
        // D10: the no-op fence — a null resolved config touches no writer (SC-007 byte-identical).
        var writer = new FakeWriter(SetupTarget.GitVersionSeed);
        var writers = new Dictionary<SetupTarget, ISetupTargetWriter> { [SetupTarget.GitVersionSeed] = writer };

        SetupProjectionResult result = SetupConfigProjector.Project(null, "/repo", writers);

        Assert.Empty(result.Written);
        Assert.Empty(writer.Received);
    }

    [Fact]
    public void All_default_resolved_calls_no_writer()
    {
        // A resolved config with only defaults projects nothing (idempotent; preserves template defaults).
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(new SetupConfig(), null, SetupAudience.New);
        var writer = new FakeWriter(SetupTarget.GitVersionSeed);
        var writers = new Dictionary<SetupTarget, ISetupTargetWriter> { [SetupTarget.GitVersionSeed] = writer };

        SetupProjectionResult result = SetupConfigProjector.Project(resolved, "/repo", writers);

        Assert.Empty(writer.Received);
        Assert.Empty(result.Written);
    }

    [Fact]
    public void Custom_field_is_dispatched_to_its_target_writer()
    {
        var config = new SetupConfig { Versioning = new SetupVersioningConfig { NextVersion = "2.0.0" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, null, SetupAudience.New);
        var writer = new FakeWriter(SetupTarget.GitVersionSeed);
        var writers = new Dictionary<SetupTarget, ISetupTargetWriter> { [SetupTarget.GitVersionSeed] = writer };

        SetupProjectionResult result = SetupConfigProjector.Project(resolved, "/repo", writers);

        Assert.Single(writer.Received);
        Assert.Equal(SetupKeys.VersioningNextVersion, writer.Received[0].Key);
        Assert.Contains("wrote:versioning.nextVersion", result.Written);
    }

    [Fact]
    public void New_only_field_on_install_audience_is_reported_ignored()
    {
        // A new-only key (description) supplied to the install audience would not be resolved at all (filtered out),
        // so to exercise the projector's ignore path we hand it a field tagged New on the Install audience directly.
        var fields = new List<ResolvedSetupField>
        {
            new(SetupKeys.IdentityDescription, SetupGroup.Identity, SetupAudience.New, SetupTarget.CsprojMetadata,
                new ConfigField("X", ConfigSource.ConfigFile, "")),
        };
        var resolved = new ResolvedSetupConfig(1, SetupAudience.Install, fields);
        var writer = new FakeWriter(SetupTarget.CsprojMetadata);
        var writers = new Dictionary<SetupTarget, ISetupTargetWriter> { [SetupTarget.CsprojMetadata] = writer };

        SetupProjectionResult result = SetupConfigProjector.Project(resolved, "/repo", writers);

        Assert.Empty(writer.Received);
        Assert.Contains(result.Ignored, i => i.Key == SetupKeys.IdentityDescription);
    }

    [Fact]
    public void Missing_writer_for_a_target_is_reported_ignored_not_thrown()
    {
        var config = new SetupConfig { Versioning = new SetupVersioningConfig { NextVersion = "2.0.0" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, null, SetupAudience.New);

        SetupProjectionResult result = SetupConfigProjector.Project(
            resolved, "/repo", new Dictionary<SetupTarget, ISetupTargetWriter>());

        Assert.Contains(result.Ignored, i => i.Key == SetupKeys.VersioningNextVersion);
        Assert.Empty(result.Written);
    }

    [Fact]
    public void None_target_keys_are_never_dispatched()
    {
        // publish.* + release.directory are SetupTarget.None — informational only, never projected to a writer.
        var config = new SetupConfig { Publish = new SetupPublishConfig { Owner = "acme" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, null, SetupAudience.New);
        var writer = new FakeWriter(SetupTarget.None);
        var writers = new Dictionary<SetupTarget, ISetupTargetWriter> { [SetupTarget.None] = writer };

        SetupProjectionResult result = SetupConfigProjector.Project(resolved, "/repo", writers);

        Assert.Empty(writer.Received);
        Assert.Empty(result.Written);
    }
}
