using System.Security.Cryptography;
using System.Text;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

public static class DotiPayloadParityChecker
{
    private static readonly string[] StaticDotiSubdirectories = ["core", "profiles", "templates", "memory", "workflows", "integrations"];

    // 032 D2(f): the vendored-tool dirs the 032 D2(e) DotiInstaller reconcile now ALSO reconciles into the temp
    // install, so the parity check can compare them the same way it compares the static .doti subdirectories —
    // closing the gap where a stale tools/sentrux manifest was recorded-managed but never CHECKED (the silently
    // broken gate this bug traces to).
    private static readonly string[] StaticToolSubdirectories = ["gitleaks", "sentrux", "gitversion"];

    public static DotiPayloadCheckResult Check(string sourceRepoRoot)
    {
        string source = Path.GetFullPath(sourceRepoRoot);
        string temp = Path.Combine(Path.GetTempPath(), "doti-payload-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            DotiInstaller.Install(source, temp, DotiAgentTarget.All, "payload-check", force: true);
            List<DotiPayloadFileStatus> files = [];
            files.AddRange(CheckStaticFiles(source, temp));
            files.AddRange(CheckToolFiles(source, temp));
            files.AddRange(CheckRenderedFiles(source, temp));
            files.AddRange(CheckSurplusSkillDirs(source));
            string[] drifted = files
                .Where(file => !file.Matches)
                .Select(file => file.InstalledPath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new DotiPayloadCheckResult(
                JsonContractDefaults.SchemaVersion,
                drifted.Length == 0 ? StageOutcome.Pass : StageOutcome.Fail,
                source,
                files.Count,
                files,
                drifted);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static IEnumerable<DotiPayloadFileStatus> CheckStaticFiles(string source, string target)
    {
        foreach (string subdirectory in StaticDotiSubdirectories)
        {
            // FR-017: `.doti/templates` is MATERIALIZED from `.doti/core/templates` — validate the installed
            // materialized copy against `core/templates` (the source of truth), even with the committed twin absent.
            // This closes the silently-skipped hole: it never depends on a source `.doti/templates` directory existing.
            if (string.Equals(subdirectory, "templates", StringComparison.OrdinalIgnoreCase))
            {
                foreach (DotiPayloadFileStatus status in CheckMaterializedTemplates(source, target))
                {
                    yield return status;
                }

                continue;
            }

            string sourceRoot = Path.Combine(source, ".doti", subdirectory);
            if (!Directory.Exists(sourceRoot))
            {
                continue;
            }

            foreach (string sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, sourceFile).Replace('\\', '/');
                string installed = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
                yield return CompareFile(sourceFile, installed, relative, relative, "static-doti");
            }
        }
    }

    /// <summary>
    /// 032 D2(f): validate the vendored-tool dirs (<c>tools/gitleaks</c>|<c>sentrux</c>|<c>gitversion</c>) the 032
    /// D2(e) install reconcile now installs — manifest/license/config/grammar files, MINUS <c>bin/</c> (the
    /// gitignored, network-fetched executable is never a parity-checked managed asset; it is verified separately by
    /// <c>SentruxManifestValidator</c>/the tool-fetch hash check). A stale/missing manifest or grammar fails
    /// <c>payload check</c> with <c>kind: "vendored-tool"</c>, closing the gap where a stale Sentrux version was
    /// recorded-managed but never actually CHECKED. Internal (not private): <see cref="Check"/> is a fresh-install
    /// SELF-consistency check (a copy from a source always reproduces it exactly, by construction), so the test seam
    /// exercises this comparison directly against hand-built source/target fixtures (see the
    /// <c>InternalsVisibleTo</c> grant in the project file).
    /// </summary>
    internal static IEnumerable<DotiPayloadFileStatus> CheckToolFiles(string source, string target)
    {
        foreach (string subdirectory in StaticToolSubdirectories)
        {
            string sourceRoot = Path.Combine(source, "tools", subdirectory);
            if (!Directory.Exists(sourceRoot))
            {
                continue;
            }

            foreach (string sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, sourceFile).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // the gitignored, per-RID vendored executable — never a parity-checked target.
                }

                string installed = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
                yield return CompareFile(sourceFile, installed, relative, relative, "vendored-tool");
            }
        }
    }

    private static IEnumerable<DotiPayloadFileStatus> CheckMaterializedTemplates(string source, string target)
    {
        string coreTemplates = Path.Combine(source, ".doti", "core", "templates");
        if (!Directory.Exists(coreTemplates))
        {
            yield break;
        }

        foreach (string coreFile in Directory.EnumerateFiles(coreTemplates, "*", SearchOption.AllDirectories))
        {
            string relativeToCore = Path.GetRelativePath(coreTemplates, coreFile).Replace('\\', '/');
            string installedRelative = ".doti/templates/" + relativeToCore;
            string installed = Path.Combine(target, installedRelative.Replace('/', Path.DirectorySeparatorChar));
            yield return CompareFile(
                coreFile, installed, ".doti/core/templates/" + relativeToCore, installedRelative, "materialized-doti");
        }
    }

    /// <summary>
    /// 027 FR-009: flag a SURPLUS managed skill dir — a <c>*doti-*</c> dir present under an agent
    /// <see cref="DotiAgentTarget.SkillsRoot"/> in the source repo that the current render no longer targets (a stage
    /// renumber that renamed the dir and left the old one). Emits one non-matching <see cref="DotiPayloadFileStatus"/>
    /// (kind <c>surplus-doti</c>) per orphan dir so <c>payload check</c> fails closed on the orphan. Scanning the
    /// source (not the freshly-installed temp, which only ever contains the render targets) is what makes this visible.
    /// </summary>
    private static IEnumerable<DotiPayloadFileStatus> CheckSurplusSkillDirs(string source)
    {
        HashSet<string> rendered = new(StringComparer.OrdinalIgnoreCase);
        foreach (DotiRenderTarget target in DotiRenderer.BuildTargets(source, DotiAgentTarget.All))
        {
            string path = target.RelativePath.Replace('\\', '/');
            foreach (DotiAgentTarget agent in DotiAgentTarget.All)
            {
                string prefix = agent.SkillsRoot.Replace('\\', '/') + "/";
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string remainder = path[prefix.Length..];
                    int slash = remainder.IndexOf('/');
                    if (slash > 0)
                    {
                        rendered.Add(prefix + remainder[..slash]);
                    }
                }
            }
        }

        foreach (DotiAgentTarget agent in DotiAgentTarget.All)
        {
            string skillsRoot = Path.Combine(source, agent.SkillsRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(skillsRoot))
            {
                continue;
            }

            foreach (string dir in Directory.GetDirectories(skillsRoot))
            {
                string dirName = Path.GetFileName(dir);
                string relDir = $"{agent.SkillsRoot}/{dirName}".Replace('\\', '/');
                if (dirName.Contains("doti-", StringComparison.OrdinalIgnoreCase) && !rendered.Contains(relDir))
                {
                    yield return new DotiPayloadFileStatus(
                        relDir, relDir, "surplus-doti", Matches: false, ExpectedSha256: null, ActualSha256: null,
                        Reason: "surplus managed skill dir present in the repo but absent from the render targets");
                }
            }
        }
    }

    private static IEnumerable<DotiPayloadFileStatus> CheckRenderedFiles(string source, string target)
    {
        UTF8Encoding utf8NoBom = new(false);
        foreach (DotiRenderTarget renderTarget in DotiRenderer.BuildTargets(source, DotiAgentTarget.All))
        {
            string installed = Path.Combine(target, renderTarget.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            byte[] expected = utf8NoBom.GetBytes(renderTarget.Content);
            yield return CompareBytes(expected, installed, renderTarget.RelativePath, renderTarget.RelativePath, "rendered-doti");
        }
    }

    private static DotiPayloadFileStatus CompareFile(
        string expectedPath,
        string actualPath,
        string sourcePath,
        string installedPath,
        string kind)
    {
        if (!File.Exists(actualPath))
        {
            return new DotiPayloadFileStatus(
                sourcePath,
                installedPath,
                kind,
                Matches: false,
                ExpectedSha256: Sha256(File.ReadAllBytes(expectedPath)),
                ActualSha256: null,
                Reason: "installed file missing");
        }

        byte[] expected = File.ReadAllBytes(expectedPath);
        return CompareBytes(expected, actualPath, sourcePath, installedPath, kind);
    }

    private static DotiPayloadFileStatus CompareBytes(
        byte[] expected,
        string actualPath,
        string sourcePath,
        string installedPath,
        string kind)
    {
        string expectedHash = Sha256(expected);
        if (!File.Exists(actualPath))
        {
            return new DotiPayloadFileStatus(sourcePath, installedPath, kind, false, expectedHash, null, "installed file missing");
        }

        byte[] actual = File.ReadAllBytes(actualPath);
        string actualHash = Sha256(actual);
        bool matches = expected.AsSpan().SequenceEqual(actual);
        return new DotiPayloadFileStatus(
            sourcePath,
            installedPath,
            kind,
            matches,
            expectedHash,
            actualHash,
            matches ? null : "installed file content differs from source payload");
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort temp cleanup; payload diagnostics have already been computed.
        }
    }
}
