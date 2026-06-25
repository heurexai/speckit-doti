using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class DotiGitIgnoreTests
{
    private const string SkillsJson =
        """
        {
          "schemaVersion": 1,
          "maturity": "command-aware-advisory",
          "commandTemplateDir": ".doti/core/templates/commands",
          "agentContextRef": ".doti/agent-context.md",
          "introTemplate": "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
          "skills": [
            { "name": "doti-specify", "description": "Spec.", "argumentHint": "[goal]", "highlights": [], "nextStage": "Run `/doti-clarify`." }
          ]
        }
        """;

    private const string ProfileJson =
        """
        { "selfHostingStatus": { "commandAvailabilityFootnote": "Footnote text.", "rootMaturityNote": "Maturity note." } }
        """;

    [Fact]
    public void EnsureCreatesGitIgnoreWithDotiRuntimeStateEntries()
    {
        string repo = NewTempDir();
        try
        {
            IReadOnlyList<string> changed = DotiGitIgnore.Ensure(repo);

            Assert.Equal([".gitignore"], changed);
            string gitignore = File.ReadAllText(Path.Combine(repo, ".gitignore"));
            Assert.Contains(".doti/cycle-state.json", gitignore);
            Assert.Contains(".doti/gate-proof.json", gitignore);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void EnsurePreservesNomosEntriesAndIsIdempotent()
    {
        string repo = NewTempDir();
        string gitignore = Path.Combine(repo, ".gitignore");
        try
        {
            File.WriteAllText(gitignore, ".nomos/cycle-state.json\n");

            IReadOnlyList<string> changed = DotiGitIgnore.Ensure(repo);
            string first = File.ReadAllText(gitignore);
            IReadOnlyList<string> secondChanged = DotiGitIgnore.Ensure(repo);
            string second = File.ReadAllText(gitignore);

            Assert.Equal([".gitignore"], changed);
            Assert.Empty(secondChanged);
            Assert.Equal(first, second);
            Assert.Contains(".nomos/cycle-state.json", first);
            Assert.Contains(".doti/cycle-state.json", first);
            Assert.Contains(".doti/gate-proof.json", first);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void DotiInstallAddsRuntimeStateIgnoreEntries()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(target, ".gitignore"), ".nomos/cycle-state.json\n");

            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "target");

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Contains(".gitignore", result.Copied);
            string gitignore = File.ReadAllText(Path.Combine(target, ".gitignore"));
            Assert.Contains(".nomos/cycle-state.json", gitignore);
            Assert.Contains(".doti/cycle-state.json", gitignore);
            Assert.Contains(".doti/gate-proof.json", gitignore);
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    private static string NewSourceRepo()
    {
        string repo = NewTempDir();
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "core", "templates", "commands"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "profiles", "dotnet-cli"));
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "skills.json"), SkillsJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "profiles", "dotnet-cli", "profile.json"), ProfileJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "templates", "agent-context-template.md"), "context body\n");
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "templates", "commands", "doti-specify.md"), "# specify\n");
        return repo;
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-gitignore-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { /* temp dir; OS cleanup is enough */ }
        catch (UnauthorizedAccessException) { /* temp dir; OS cleanup is enough */ }
    }
}
