using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    /// <summary>
    /// 007 T023 build/release tool: generate <c>payload.manifest.json</c> (the source-free <see cref="PayloadDescriptor"/>)
    /// for a staged payload root, so the packed global tool / MSIX resolves its payload source-free + tamper-evidently
    /// (<see cref="PayloadRoot.Resolve()"/>). Invoked by the pack pipeline; not an everyday operator command.
    /// </summary>
    public static CliResult PayloadManifest(
        CliMeta meta, string root, string payloadVersion, string toolVersion, string channel, string mode)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return CliResults.Fail(meta, "doti payload-manifest", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, $"Staged payload root not found: {root}")]);
        }

        PayloadDescriptor descriptor = PayloadManifestGenerator.Write(
            Path.GetFullPath(root), payloadVersion, toolVersion, channel, mode);

        return CliResults.Ok(meta, "doti payload-manifest",
            $"payload.manifest.json written: {descriptor.FileHashes.Count} file(s), channel={descriptor.Channel}, payload={descriptor.PayloadVersion}.",
            new
            {
                channel = descriptor.Channel,
                mode = descriptor.Mode,
                payloadVersion = descriptor.PayloadVersion,
                toolVersion = descriptor.ToolVersion,
                fileCount = descriptor.FileHashes.Count,
            });
    }
}
