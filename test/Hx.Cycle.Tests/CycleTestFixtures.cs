using Hx.Cycle.Core;

namespace Hx.Cycle.Tests;

/// <summary>Shared, git-free fixtures for the cycle unit tests: a temp repo dir, a minimal two-stage
/// <see cref="StageModel"/> (specify → plan), and an artifact-writing helper that returns the canonical hash.</summary>
internal static class CycleTestFixtures
{
    public const string Feature = "001-test";

    public static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-cycle-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static StageModel TwoStageModel(string dir)
    {
        string yml = Path.Combine(dir, "workflow.yml");
        File.WriteAllText(yml,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: c\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: plan\n    command: c\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [specify]\n");
        return StageModel.Load(yml);
    }

    public static string Write(string dir, string relative, string content)
    {
        string full = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return CanonicalArtifactHasher.CanonicalHashOfText(content);
    }
}
