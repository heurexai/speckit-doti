using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Hx.Scaffold.Core.Release;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    private static ReleaseManifestUpdatePlan PlanReleaseManifest(string gitRoot)
    {
        string manifestPath = Path.Combine(gitRoot, ReleaseTargetManifest.RelativePath);
        if (File.Exists(manifestPath))
        {
            return ReleaseManifestUpdatePlan.Preserved;
        }

        ReleaseProjectCandidate[] candidates = Directory
            .GetFiles(gitRoot, "*.csproj", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(gitRoot, path).Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .Where(path => !IsIgnoredProjectPath(gitRoot, path))
            .Select(path => ReadReleaseProjectCandidate(gitRoot, path))
            .Where(candidate => candidate is not null)
            .Cast<ReleaseProjectCandidate>()
            .Where(candidate => candidate.IsExecutable)
            .ToArray();

        if (candidates.Length != 1)
        {
            string reason = candidates.Length == 0
                ? "no executable .csproj was found"
                : $"multiple executable .csproj files were found: {string.Join(", ", candidates.Select(c => c.RelativeProjectPath))}";
            return ReleaseManifestUpdatePlan.NeedsManual(
                $"Create .doti/release.json before running hx release; {reason}, so update did not guess the release target.");
        }

        ReleaseProjectCandidate candidate = candidates.Single();
        var document = new
        {
            schemaVersion = 1,
            productName = candidate.ExecutableName,
            packageName = candidate.ExecutableName,
            publishProject = candidate.RelativeProjectPath,
            publishedExecutableName = candidate.ExecutableName,
            executableName = candidate.ExecutableName,
            defaultReleaseRootEnvironmentVariable = LocalReleaseRootResolver.DefaultEnvironmentVariableName
        };
        byte[] content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(JsonSerializer.Serialize(document, JsonContractSerializerOptions.Create()));

        return new ReleaseManifestUpdatePlan(
            ShouldCreate: true,
            Content: content,
            FollowUp: $"Update created .doti/release.json from the only executable project ({candidate.RelativeProjectPath}); review product/package names before release if this repo uses a custom executable identity.");
    }

    private static IReadOnlyList<string> ApplyReleaseManifest(string gitRoot, ReleaseManifestUpdatePlan plan)
    {
        if (!plan.ShouldCreate || plan.Content is null)
        {
            return [];
        }

        string full = Path.Combine(gitRoot, ReleaseTargetManifest.RelativePath);
        if (File.Exists(full))
        {
            return [];
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, plan.Content);
        return [ReleaseTargetManifest.RelativePath];
    }

    private static bool IsIgnoredProjectPath(string gitRoot, string path)
    {
        string relative = Path.GetRelativePath(gitRoot, path).Replace('\\', '/');
        return relative.StartsWith("tools/", StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("test/", StringComparison.OrdinalIgnoreCase)
            || relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static ReleaseProjectCandidate? ReadReleaseProjectCandidate(string gitRoot, string path)
    {
        try
        {
            XDocument document = XDocument.Load(path, LoadOptions.None);
            string outputType = document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "OutputType")
                ?.Value
                ?.Trim()
                ?? "";
            bool executable = outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase)
                || outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase);
            if (!executable)
            {
                return null;
            }

            string assemblyName = document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "AssemblyName")
                ?.Value
                ?.Trim()
                ?? "";
            string executableName = SafeReleaseName(
                string.IsNullOrWhiteSpace(assemblyName)
                    ? Path.GetFileNameWithoutExtension(path)
                    : assemblyName);
            string relative = Path.GetRelativePath(gitRoot, path).Replace('\\', '/');
            return new ReleaseProjectCandidate(relative, executableName, IsExecutable: true);
        }
        catch
        {
            return null;
        }
    }

    private static string SafeReleaseName(string value)
    {
        string safe = new(value.Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-').ToArray());
        safe = safe.Trim('.', '-', '_');
        return string.IsNullOrWhiteSpace(safe) ? "app" : safe;
    }

    private sealed record ReleaseManifestUpdatePlan(bool ShouldCreate, byte[]? Content, string? FollowUp)
    {
        public static ReleaseManifestUpdatePlan Preserved { get; } = new(false, null, null);

        public static ReleaseManifestUpdatePlan NeedsManual(string followUp) => new(false, null, followUp);
    }

    private sealed record ReleaseProjectCandidate(string RelativeProjectPath, string ExecutableName, bool IsExecutable);
}
