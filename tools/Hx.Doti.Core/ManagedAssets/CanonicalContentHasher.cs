using System.Security.Cryptography;
using System.Text;

namespace Hx.Doti.Core.ManagedAssets;

public static class HashProfile
{
    public const string ByteExact = "byte-exact/v1";
    public const string NormalizedText = "normalized-text/v2";
    public const string LegacyWhitespaceDeletedText = "normalized-text/v1";
    public const string JsonSemantic = "json-rfc8785-compatible/v1";
    public const string YamlSemantic = "yaml-representation/v1";
}

public sealed record CanonicalHash(string Profile, string Sha256, string SourceFormat, string Canonicalizer);

public static partial class CanonicalContentHasher
{
    public static CanonicalHash HashFile(string path, string profile)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return profile switch
        {
            HashProfile.ByteExact => new CanonicalHash(profile, Sha256(bytes), SourceFormatForPath(path), "byte-exact"),
            HashProfile.NormalizedText => new CanonicalHash(profile, Sha256(Encoding.UTF8.GetBytes(NormalizeText(bytes))), "text", "speckit-doti-text-normalizer/v2"),
            HashProfile.LegacyWhitespaceDeletedText => new CanonicalHash(profile, Sha256(Encoding.UTF8.GetBytes(DeleteWhitespace(bytes))), "text", "speckit-doti-text-normalizer/v1"),
            HashProfile.JsonSemantic => new CanonicalHash(profile, Sha256(Encoding.UTF8.GetBytes(CanonicalizeJson(bytes))), "json", "System.Text.Json+rfc8785-compatible/v1"),
            HashProfile.YamlSemantic => new CanonicalHash(profile, Sha256(Encoding.UTF8.GetBytes(CanonicalizeYaml(bytes))), "yaml", "YamlDotNet-representation/v1"),
            _ => throw new InvalidOperationException($"Unsupported managed-asset hash profile '{profile}'."),
        };
    }

    public static string ProfileForPath(string relativePath)
    {
        string ext = Path.GetExtension(relativePath);
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return HashProfile.JsonSemantic;
        }

        if (ext.Equals(".yml", StringComparison.OrdinalIgnoreCase) || ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            return HashProfile.YamlSemantic;
        }

        if (ext.Equals(".md", StringComparison.OrdinalIgnoreCase) || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return HashProfile.NormalizedText;
        }

        return HashProfile.ByteExact;
    }

    private static string NormalizeText(byte[] bytes)
    {
        string text = DecodeUtf8(bytes);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string DeleteWhitespace(byte[] bytes)
    {
        string text = DecodeUtf8(bytes);
        var sb = new StringBuilder(text.Length);
        foreach (char ch in text.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (!char.IsWhiteSpace(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            throw new InvalidOperationException("UTF-8 BOM is not supported for canonical managed-asset hashes.");
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string SourceFormatForPath(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(ext) ? "binary" : ext;
    }
}
