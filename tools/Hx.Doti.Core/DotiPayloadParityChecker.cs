using System.Security.Cryptography;
using System.Text;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

public static class DotiPayloadParityChecker
{
    private static readonly string[] StaticDotiSubdirectories = ["core", "profiles", "templates", "memory", "workflows", "integrations"];

    public static DotiPayloadCheckResult Check(string sourceRepoRoot)
    {
        string source = Path.GetFullPath(sourceRepoRoot);
        string temp = Path.Combine(Path.GetTempPath(), "doti-payload-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            DotiInstaller.Install(source, temp, DotiAgentTarget.All, "payload-check", force: true);
            List<DotiPayloadFileStatus> files = [];
            files.AddRange(CheckStaticFiles(source, temp));
            files.AddRange(CheckRenderedFiles(source, temp));
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
