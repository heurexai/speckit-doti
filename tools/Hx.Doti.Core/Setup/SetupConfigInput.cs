using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>029 FR-002/FR-009: the outcome of resolving a <c>--config</c> input — the <see cref="ResolvedSetupConfig"/>
/// when valid (or the no-config <c>null</c>), or the validation errors (each naming the offending field) when not.</summary>
public sealed record SetupResolveOutcome(ResolvedSetupConfig? Resolved, IReadOnlyList<SetupValidationError> Errors)
{
    public bool Ok => Errors.Count == 0;

    /// <summary>The no-config result — nothing resolved, no errors (D10 byte-identical path).</summary>
    public static readonly SetupResolveOutcome None = new(null, []);
}

/// <summary>
/// 029 D2/D5: the SHARED setup-config input flow both CLI hosts (<c>hx new</c>, <c>hx doti install</c>) drive — load +
/// schema-validate a <c>--config</c> file and resolve it (or resolve a wizard-produced <see cref="SetupConfig"/>)
/// against the documented defaults for the host's audience. Genuine reusable logic, so it lives in <c>Doti.Core</c>
/// (over the pure <see cref="SetupConfigLoader"/>/<see cref="SetupConfigResolver"/>) rather than the thin CLI surface —
/// confining the loader/resolver/load-result type fan-out away from the command files.
/// </summary>
public static class SetupConfigInput
{
    /// <summary>Resolve a wizard-collected config (the <c>--interactive</c> path) — the same resolve <c>--config</c>
    /// uses, tagged <see cref="ConfigSource.Interactive"/> so the two inputs are provably 1:1 (SC-004).</summary>
    public static ResolvedSetupConfig ResolveInteractive(
        SetupConfig wizardConfig, SetupFlagOverrides flags, SetupAudience audience) =>
        SetupConfigResolver.Resolve(wizardConfig, flags, audience, ConfigSource.Interactive);

    /// <summary>Load + validate the <c>--config</c> file and resolve it for <paramref name="audience"/>; a blank
    /// <paramref name="configPath"/> is the no-config path (<see cref="SetupResolveOutcome.None"/>, D10).</summary>
    public static SetupResolveOutcome ResolveFromFile(
        string? configPath, string baseDirectory, SetupFlagOverrides flags, SetupAudience audience)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return SetupResolveOutcome.None;
        }

        SetupConfigLoadResult load = SetupConfigLoader.Load(configPath, baseDirectory);
        if (!load.Ok)
        {
            return new SetupResolveOutcome(null, load.Errors);
        }

        return new SetupResolveOutcome(SetupConfigResolver.Resolve(load.Config, flags, audience), []);
    }
}
