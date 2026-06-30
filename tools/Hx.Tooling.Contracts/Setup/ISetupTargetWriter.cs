namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 D3: one projection target's writer. The pure <see cref="SetupConfigProjector"/> (Contracts) iterates the
/// key→target table and dispatches to the writer the caller injected for that <see cref="SetupTarget"/>; the concrete
/// implementations live where the asset lives (Doti.Core for <c>.doti</c> assets, Scaffold.Core for project files), so
/// no forbidden edge/cycle is required. A writer receives only the custom (operator-supplied) fields for its target.</summary>
public interface ISetupTargetWriter
{
    /// <summary>The target this writer projects into.</summary>
    SetupTarget Target { get; }

    /// <summary>Write the resolved <paramref name="fields"/> (all sharing <see cref="Target"/>, all custom) into
    /// <paramref name="repositoryRoot"/>, returning the relative paths touched (for the effect report). Idempotent.</summary>
    IReadOnlyList<string> Write(string repositoryRoot, IReadOnlyList<ResolvedSetupField> fields);
}
