namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 FR-001: which layer supplied a resolved key's value — first-class provenance, never reconstructed.</summary>
public enum ConfigSource
{
    /// <summary>The documented built-in default (no operator input).</summary>
    Default,

    /// <summary>A value read from the <c>--config</c> / <c>.doti/setup.json</c> file.</summary>
    ConfigFile,

    /// <summary>A value collected by the <c>--interactive</c> wizard.</summary>
    Interactive,

    /// <summary>A value supplied by an explicit CLI flag (<c>--name</c>/<c>--company</c>/<c>--output</c>/<c>--agents</c>) — highest priority.</summary>
    Flag,

    /// <summary>A value computed from other inputs (e.g. authors defaulting to company).</summary>
    Derived,
}

/// <summary>029 FR-001: which group a setting drives (the human-table grouping; single-sourced on the key so it is not re-derived).</summary>
public enum SetupGroup
{
    Identity,
    Versioning,
    Release,
    Publish,
    Agents,
    Constitution,
}

/// <summary>029 C3: which host command(s) a key applies to. <c>install</c> consumes the doti-layer subset; new-only keys reached on install are reported as ignored.</summary>
public enum SetupAudience
{
    /// <summary>Only <c>hx new</c> (project generation: e.g. <c>output</c>).</summary>
    New,

    /// <summary>Only <c>hx doti install</c>.</summary>
    Install,

    /// <summary>Both host commands.</summary>
    Both,
}
