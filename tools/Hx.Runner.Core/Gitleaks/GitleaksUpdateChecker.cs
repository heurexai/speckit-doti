using System.Net.Http;
using System.Text.Json;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Gitleaks;

/// <summary>
/// Explicit, network-enabled Gitleaks update check. Compares the pinned manifest
/// version against the upstream latest release. Network failures are reported as
/// notes, never thrown, so the command degrades gracefully.
/// </summary>
public static class GitleaksUpdateChecker
{
    public static GitleaksUpdateCheck Check(string repositoryRoot)
    {
        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(
            repositoryRoot, GitleaksManifestValidator.ManifestRelativePath);

        if (!File.Exists(manifestPath.FullPath))
        {
            return new GitleaksUpdateCheck(
                JsonContractDefaults.SchemaVersion, false, false, null, null, null, null,
                ["Gitleaks is not vendored yet; nothing to update-check."]);
        }

        GitleaksManifest? manifest = JsonSerializer.Deserialize<GitleaksManifest>(
            File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create());
        if (manifest is null)
        {
            return new GitleaksUpdateCheck(
                JsonContractDefaults.SchemaVersion, true, false, null, null, null, null,
                ["Gitleaks manifest is invalid."]);
        }

        List<string> notes = [];
        string? latest = null;
        bool performed = false;
        bool? updateAvailable = null;

        try
        {
            string api = manifest.Repository.TrimEnd('/')
                .Replace("https://github.com/", "https://api.github.com/repos/", StringComparison.OrdinalIgnoreCase)
                + "/releases/latest";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("scaffold-dotnet-hygiene");
            string body = http.GetStringAsync(api).GetAwaiter().GetResult();

            using JsonDocument document = JsonDocument.Parse(body);
            latest = document.RootElement.TryGetProperty("tag_name", out JsonElement tag)
                ? tag.GetString()
                : null;
            performed = true;
            updateAvailable = latest is not null
                && !string.Equals(latest, manifest.Version, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            notes.Add("Network update check failed: " + ex.Message);
        }

        return new GitleaksUpdateCheck(
            JsonContractDefaults.SchemaVersion, true, performed,
            manifest.Version, latest, updateAvailable, manifest.ReleaseUrl, notes);
    }
}
