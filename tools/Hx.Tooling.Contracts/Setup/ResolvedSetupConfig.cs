namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 FR-001: one resolved key — its effective <see cref="Value"/>, the <see cref="Source"/> that supplied
/// it, and the <see cref="Default"/> it would otherwise take. The <c>{value, source, default}</c> shape <c>config
/// show --json</c> emits and the human table renders.</summary>
public sealed record ConfigField(string Value, ConfigSource Source, string Default);

/// <summary>
/// 029 FR-001/D1/D2: the provenance-tracked resolution — every operator-configurable key carries a
/// <see cref="ConfigField"/>. This is the SINGLE model both input paths and the show surface consume (no second
/// resolution path). <see cref="Fields"/> is an ordered list keyed by <see cref="ConfigField"/> id so it serializes
/// deterministically and additively (null/omitted on the no-config path → SC-007 shape preserved). Pure; no IO.
/// </summary>
public sealed record ResolvedSetupConfig(
    int SchemaVersion,
    SetupAudience Audience,
    IReadOnlyList<ResolvedSetupField> Fields)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>The resolved field for <paramref name="id"/>, or <c>null</c> when the audience excludes it.</summary>
    public ResolvedSetupField? Find(string id) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, id, StringComparison.Ordinal));

    /// <summary>The effective value for <paramref name="id"/>, or its default when the key is absent.</summary>
    public string ValueOrDefault(string id) =>
        Find(id)?.Field.Value ?? SetupKeys.ById_(id).Default;

    /// <summary>Whether <paramref name="id"/> was operator-supplied (source != default).</summary>
    public bool IsCustom(string id) => Find(id)?.Field.Source is { } s && s != ConfigSource.Default;
}

/// <summary>One resolved key paired with its descriptor metadata (group/audience/target) for the projector + formatter.</summary>
public sealed record ResolvedSetupField(
    string Key,
    SetupGroup Group,
    SetupAudience Audience,
    SetupTarget Target,
    ConfigField Field);
