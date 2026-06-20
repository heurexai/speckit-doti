using System.Text.Json;
using Hx.Runner.Core.Io;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

/// <summary>
/// Stages the vendored, repo-local Sentrux grammar(s) into the Sentrux plugins
/// directory (<c>~/.sentrux/plugins/&lt;lang&gt;/grammars/</c>) so that — with
/// <c>SENTRUX_SKIP_GRAMMAR_DOWNLOAD</c> set — analysis uses the pinned repo grammar
/// on any machine, instead of silently depending on a pre-existing global install
/// or a network download. Sentrux writes the embedded plugin.toml/queries itself;
/// only the grammar binary must be provided.
/// </summary>
public static class SentruxGrammarStager
{
    public static string DefaultPluginsDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sentrux", "plugins");

    public static IReadOnlyList<string> EnsureStaged(string repositoryRoot, string hostRuntimeIdentifier) =>
        EnsureStagedTo(repositoryRoot, hostRuntimeIdentifier, DefaultPluginsDir());

    /// <summary>Copy each manifest grammar for the host RID into <paramref name="pluginsDir"/> (idempotent).</summary>
    public static IReadOnlyList<string> EnsureStagedTo(string repositoryRoot, string hostRuntimeIdentifier, string pluginsDir)
    {
        List<string> staged = [];
        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(
            repositoryRoot, SentruxManifestValidator.ManifestRelativePath);
        if (!File.Exists(manifestPath.FullPath))
        {
            return staged;
        }

        SentruxManifest? manifest = JsonSerializer.Deserialize<SentruxManifest>(
            File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create());
        if (manifest is null)
        {
            return staged;
        }

        foreach (SentruxGrammar grammar in manifest.Grammars.Where(g =>
                     string.Equals(g.Rid, hostRuntimeIdentifier, StringComparison.OrdinalIgnoreCase)))
        {
            RepositoryPath source = RepositoryPathResolver.ResolveInside(repositoryRoot, grammar.Path);
            if (!File.Exists(source.FullPath))
            {
                continue;
            }

            string target = Path.Combine(pluginsDir, grammar.Name, "grammars", Path.GetFileName(source.FullPath));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (!File.Exists(target)
                || !string.Equals(FileHashing.Sha256OfFile(target), FileHashing.Sha256OfFile(source.FullPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(source.FullPath, target, overwrite: true);
                staged.Add($"{grammar.Name} -> {target}");
            }
        }

        return staged;
    }
}
