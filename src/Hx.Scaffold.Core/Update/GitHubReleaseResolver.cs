using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hx.Scaffold.Core.Update;

internal static class GitHubReleaseResolver
{
    public static UpdateRelease ResolveLatest(string rid)
    {
        string json = Encoding.UTF8.GetString(DownloadBytes(new Uri("https://api.github.com/repos/heurexai/speckit-doti/releases/latest")));
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("Latest GitHub release has no tag_name.");
        if (root.TryGetProperty("prerelease", out JsonElement prerelease) && prerelease.GetBoolean())
        {
            throw new InvalidOperationException("Latest GitHub release is a prerelease; refusing default latest mode.");
        }

        string assetName = ExpectedAssetName(tag, rid);
        UpdateReleaseAsset? archive = null;
        UpdateReleaseAsset? checksum = null;
        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            string? url = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
            {
                archive = new UpdateReleaseAsset(name, new Uri(url));
            }
            else if (string.Equals(name, assetName + ".sha256", StringComparison.OrdinalIgnoreCase))
            {
                checksum = new UpdateReleaseAsset(name, new Uri(url));
            }
        }

        if (archive is null)
        {
            throw new InvalidOperationException($"No release asset '{assetName}' found for host RID '{rid}'.");
        }

        if (checksum is null)
        {
            throw new InvalidOperationException($"No checksum asset '{assetName}.sha256' found for host RID '{rid}'.");
        }

        string version = tag.TrimStart('v', 'V');
        return new UpdateRelease(tag, version, archive, checksum);
    }

    public static byte[] DownloadBytes(Uri uri)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("speckit-doti-hx-update", "1.0"));
        return http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
    }

    private static string ExpectedAssetName(string tag, string rid)
    {
        string ext = OperatingSystem.IsWindows() ? "zip" : "tar.gz";
        return $"speckit-doti-{tag}-{rid}.{ext}";
    }
}
