using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>
/// 029 FR-006/D3: projects <c>versioning.nextVersion</c> into <c>GitVersion.yml</c>'s <c>next-version</c>. Replaces
/// the existing <c>next-version:</c> line in place (a targeted line edit, NOT a YAML round-trip) so the template's
/// extensive guidance comments + the <c>workflow</c> line are preserved verbatim. The value is a validated 3-part
/// numeric SemVer core (<see cref="SetupConfigSchema.TryParseSemVerCore"/>), so it carries no YAML-injection risk.
/// Idempotent.
/// </summary>
public sealed class GitVersionSeedWriter : ISetupTargetWriter
{
    public const string RelativePath = "GitVersion.yml";

    public SetupTarget Target => SetupTarget.GitVersionSeed;

    public IReadOnlyList<string> Write(string repositoryRoot, IReadOnlyList<ResolvedSetupField> fields)
    {
        ResolvedSetupField? field = fields.FirstOrDefault(f => f.Key == SetupKeys.VersioningNextVersion);
        if (field is null)
        {
            return [];
        }

        string value = field.Field.Value.Trim();
        if (!SetupConfigSchema.TryParseSemVerCore(value, out _, out _, out _))
        {
            // Defense in depth: the CLI validated before generation; refuse a non-SemVer here rather than corrupt YAML.
            throw new InvalidOperationException($"Refusing to write a non-SemVer next-version '{value}' to GitVersion.yml.");
        }

        string path = SetupAssetPaths.ResolveInside(repositoryRoot, RelativePath);
        if (!File.Exists(path))
        {
            return [];
        }

        string[] lines = File.ReadAllLines(path);
        bool replaced = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("next-version:", StringComparison.Ordinal))
            {
                string desired = $"next-version: {value}";
                if (string.Equals(lines[i], desired, StringComparison.Ordinal))
                {
                    return []; // already correct — idempotent no-op.
                }

                lines[i] = desired;
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            return [];
        }

        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        return [RelativePath];
    }
}
