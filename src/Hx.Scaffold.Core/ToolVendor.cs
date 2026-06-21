using System.Text.Json;
using Hx.Runner.Core.Gitleaks;
using Hx.Runner.Core.Hygiene;
using Hx.Runner.Core.Io;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>
/// Copies the scaffold's already release-sourced, SHA-256-hashed, manifest-pinned Gitleaks + Sentrux +
/// GitVersion tool trees into a generated repo (binaries when present, manifest, grammars, LICENSE). The
/// copied manifest travels with the tool so the generated repo can re-verify it (the smoke does this) and
/// re-fetch any missing binary from its pinned downloadUrl (<see cref="Hx.Runner.Core.Tools.ToolFetcher"/>).
/// The Gitleaks native config is then re-rendered from the GENERATED repo's rules/hygiene.json (plan:
/// rendered, not copied) and its hash re-pinned so <c>gitleaks verify</c> stays green.
/// </summary>
public static class ToolVendor
{
    public static readonly IReadOnlyList<string> ToolDirectories = ["tools/gitleaks", "tools/sentrux", "tools/gitversion"];

    public static void Vendor(string sourceRepoRoot, string targetRepoRoot)
    {
        foreach (string relative in ToolDirectories)
        {
            string from = Path.Combine(sourceRepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            string to = Path.Combine(targetRepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(from))
            {
                throw new DirectoryNotFoundException(
                    $"Vendored tool source is missing in the scaffold: {relative}. Vendor it before scaffolding.");
            }

            // Include everything (bin, config, grammars, *.version.json, LICENSE) — these ARE the
            // vendored artifacts the generated repo verifies.
            DirectoryCopy.Copy(from, to, _ => true);
        }

        RefreshGitleaksConfig(targetRepoRoot);
    }

    /// <summary>
    /// Re-renders <c>tools/gitleaks/config/gitleaks.toml</c> from the generated repo's
    /// <c>rules/hygiene.json</c> (its allowlist differs from the scaffold's) and re-pins
    /// <c>configSha256</c> in the copied manifest, using the same hash helper the verifier uses.
    /// </summary>
    private static void RefreshGitleaksConfig(string targetRepoRoot)
    {
        HygienePolicy policy = HygienePolicyLoader.Load(targetRepoRoot, out _);
        string configPath = Path.Combine(targetRepoRoot, "tools", "gitleaks", "config", "gitleaks.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, GitleaksConfigRenderer.Render(policy));

        string manifestPath = Path.Combine(targetRepoRoot, "tools", "gitleaks", "gitleaks.version.json");
        GitleaksManifest manifest = JsonSerializer.Deserialize<GitleaksManifest>(
            File.ReadAllText(manifestPath), JsonContractSerializerOptions.Create())
            ?? throw new InvalidOperationException("Vendored Gitleaks manifest is empty.");
        GitleaksManifest updated = manifest with { ConfigSha256 = FileHashing.Sha256OfFile(configPath) };

        // A fresh options instance: Deserialize above locked the first one (read-only after use).
        JsonSerializerOptions writeOptions = JsonContractSerializerOptions.Create();
        writeOptions.WriteIndented = true;
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(updated, writeOptions));
    }
}
