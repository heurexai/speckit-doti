using Hx.Sentrux.Core;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxGrammarStagerTests
{
    private const string Manifest =
        """
        {
          "schemaVersion": 1,
          "tool": "sentrux",
          "license": "MIT",
          "sourceRemote": "https://github.com/heurexai/sentrux",
          "releaseTag": "v0.5.8",
          "sourceCommit": "",
          "distributionIdentity": "Heurex fork",
          "updateChannel": "stable",
          "vendoredAt": "2026-06-17",
          "assets": [],
          "grammars": [
            { "name": "csharp", "rid": "win-x64", "path": "tools/sentrux/grammars/csharp/grammars/windows-x86_64.dll", "sha256": null }
          ],
          "requiredFeatures": ["check-include-untracked", "gate-save"]
        }
        """;

    [Fact]
    public void StagesVendoredGrammarIntoPluginsDirAndIsIdempotent()
    {
        string repo = Path.Combine(Path.GetTempPath(), "hx-sxg-" + Guid.NewGuid().ToString("n"));
        string plugins = Path.Combine(Path.GetTempPath(), "hx-sxgp-" + Guid.NewGuid().ToString("n"));
        string grammarDir = Path.Combine(repo, "tools", "sentrux", "grammars", "csharp", "grammars");
        Directory.CreateDirectory(grammarDir);
        Directory.CreateDirectory(Path.Combine(repo, "tools", "sentrux"));
        File.WriteAllText(Path.Combine(grammarDir, "windows-x86_64.dll"), "fake-grammar-bytes");
        File.WriteAllText(Path.Combine(repo, "tools", "sentrux", "sentrux.version.json"), Manifest);

        try
        {
            IReadOnlyList<string> staged = SentruxGrammarStager.EnsureStagedTo(repo, "win-x64", plugins);

            string target = Path.Combine(plugins, "csharp", "grammars", "windows-x86_64.dll");
            Assert.True(File.Exists(target));
            Assert.Contains(staged, s => s.Contains("csharp", StringComparison.Ordinal));

            // Second run is idempotent (identical content already staged).
            Assert.Empty(SentruxGrammarStager.EnsureStagedTo(repo, "win-x64", plugins));
        }
        finally
        {
            if (Directory.Exists(repo)) Directory.Delete(repo, recursive: true);
            if (Directory.Exists(plugins)) Directory.Delete(plugins, recursive: true);
        }
    }
}
