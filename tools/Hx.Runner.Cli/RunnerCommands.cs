using Hx.Cli.Kernel;
using Hx.Runner.Core;
using Hx.Runner.Core.Platform;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

/// <summary>
/// The Runner CLI's command bodies: each maps a deterministic core result onto the <see cref="CliResult"/> envelope.
/// Kept out of <c>Program.cs</c> wiring so the mappings are unit-testable in-process (the repo's "test the core, not
/// the exe" convention). Exit semantics: a check/gate that did not pass ⇒ <see cref="ExitClass.Validation"/>; a tool
/// integrity verification failure ⇒ <see cref="ExitClass.Integrity"/>; bad invocation ⇒ <see cref="ExitClass.Usage"/>;
/// an unexpected exception fail-closes (the host maps it to Internal).
/// </summary>
public static partial class RunnerCommands
{
    // ---- bootstrap / platform ----

    public static CliResult BootstrapProof(CliMeta meta) =>
        CliResults.Ok(meta, "bootstrap-proof", "Bootstrap advisory proof.", GateProofFactory.BootstrapAdvisoryProof());

    public static CliResult PlatformProbe(CliMeta meta) =>
        CliResults.Ok(meta, "platform probe", "Cross-platform diagnostics.", CrossPlatformProbe.Create());

    // ---- shared helpers ----

    private static CliResult Verify(CliMeta meta, string command, ToolVerificationResult result) =>
        CliResults.FromStage(meta, command, result.Outcome,
            result.Verified ? "Verified." : string.Join("; ", result.Problems), result, ExitClass.Integrity);

    private static CliResult Usage(CliMeta meta, string command, string message) =>
        CliResults.Fail(meta, command, ExitClass.Usage, [Diag.Of(ErrorCodes.Usage_InvalidArguments, message)]);

    private static string Rid() => HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
}
