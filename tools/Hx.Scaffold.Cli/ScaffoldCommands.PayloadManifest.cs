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
        CliMeta meta, string root, string payloadVersion, string toolVersion, string channel, string mode, string digestOut)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return CliResults.Fail(meta, "doti payload-manifest", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, $"Staged payload root not found: {root}")]);
        }

        string stagedRoot = Path.GetFullPath(root);
        PayloadDescriptor descriptor = PayloadManifestGenerator.Write(
            stagedRoot, payloadVersion, toolVersion, channel, mode);

        // FR-003 / C2 anti-substitution anchor: compute the manifest digest EXACTLY as PayloadRoot.Resolve() will
        // (Sha256OfText of the written descriptor) so the two-phase pack can embed it in the executable. When a
        // --digest-out path is given, write the digest there for the build to read back.
        string manifestPath = Path.Combine(stagedRoot, PayloadRoot.ManifestFileName);
        string manifestSha256 = PayloadRoot.Sha256OfText(File.ReadAllText(manifestPath));
        if (!string.IsNullOrWhiteSpace(digestOut))
        {
            string outPath = Path.GetFullPath(digestOut);
            string? outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            File.WriteAllText(outPath, manifestSha256);
        }

        return CliResults.Ok(meta, "doti payload-manifest",
            $"payload.manifest.json written: {descriptor.FileHashes.Count} file(s), channel={descriptor.Channel}, payload={descriptor.PayloadVersion}.",
            new
            {
                channel = descriptor.Channel,
                mode = descriptor.Mode,
                payloadVersion = descriptor.PayloadVersion,
                toolVersion = descriptor.ToolVersion,
                fileCount = descriptor.FileHashes.Count,
                manifestSha256,
            });
    }
}
