using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

/// <summary>
/// The Scaffold CLI's command bodies: map generation onto the <see cref="CliResult"/> envelope. Kept out of
/// <c>Program.cs</c> wiring so the mapping is unit-testable in-process. A successful <c>new</c> carries the generated
/// repo as an Effect; a missing <c>--name</c>/<c>--output</c> is a Usage error; a generation/smoke failure is a
/// Validation failure with the <see cref="ScaffoldProof"/> preserved in <c>data</c>.
/// </summary>
public static partial class ScaffoldCommands
{
    public static CliResult Profile(CliMeta meta) =>
        CliResults.Ok(meta, "profile", $"Default scaffold profile: {ScaffoldBootstrap.DefaultProfile.Name}.",
            ScaffoldBootstrap.DefaultProfile);
}
