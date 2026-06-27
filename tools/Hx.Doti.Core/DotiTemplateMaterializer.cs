namespace Hx.Doti.Core;

/// <summary>
/// FR-014/015/016: materialize the installed/shipped <c>.doti/templates</c> from the single source
/// <c>.doti/core/templates</c>. The committed <c>.doti/templates</c> twin is removed (gitignored); this makes
/// <c>core/templates</c> causally upstream so drift is structurally impossible, not test-policed. The materializer is
/// the SOLE writer of <c>.doti/templates</c> — it cleans the target first (H-6), so a stale local copy can never leak
/// into the anchored payload.
/// </summary>
public static class DotiTemplateMaterializer
{
    public const string SourceRelative = ".doti/core/templates";
    public const string TargetRelative = ".doti/templates";

    /// <summary>Materialize under a single repo/payload root (<c>.doti/core/templates</c> → <c>.doti/templates</c>).</summary>
    public static IReadOnlyList<string> Materialize(string root) =>
        MaterializeFromTo(
            Path.Combine(root, ".doti", "core", "templates"),
            Path.Combine(root, ".doti", "templates"));

    /// <summary>Materialize from an explicit source-templates dir to an explicit target-templates dir, cleaning the
    /// target first so it is sole-writer. Returns the repo-relative materialized paths.</summary>
    public static IReadOnlyList<string> MaterializeFromTo(string sourceTemplates, string targetTemplates)
    {
        if (Directory.Exists(targetTemplates))
        {
            Directory.Delete(targetTemplates, recursive: true);
        }

        if (!Directory.Exists(sourceTemplates))
        {
            return [];
        }

        var written = new List<string>();
        foreach (string file in Directory.EnumerateFiles(sourceTemplates, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceTemplates, file);
            string destination = Path.Combine(targetTemplates, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
            written.Add(".doti/templates/" + relative.Replace('\\', '/'));
        }

        return written;
    }
}
