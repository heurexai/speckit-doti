using System.Text.Json;
using System.Text.Json.Nodes;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>
/// 029 FR-006/D3: projects <c>release.environmentVariable</c> into <c>.doti/release.json</c>'s
/// <c>defaultReleaseRootEnvironmentVariable</c>. The manifest is already written (with its derived defaults) by the
/// scaffold/install path before this runs, so the writer reads it as a <see cref="JsonObject"/> and updates ONLY the
/// env-var field, preserving every other (derived) property — no whole-manifest re-derivation, no <c>Scaffold.Core</c>
/// edge. The value is a validated env-var name. Idempotent; a no-op when the manifest is absent.
/// </summary>
public sealed class ReleaseTargetWriter : ISetupTargetWriter
{
    public const string RelativePath = ".doti/release.json";
    private const string EnvVarProperty = "defaultReleaseRootEnvironmentVariable";

    public SetupTarget Target => SetupTarget.ReleaseManifest;

    public IReadOnlyList<string> Write(string repositoryRoot, IReadOnlyList<ResolvedSetupField> fields)
    {
        ResolvedSetupField? field = fields.FirstOrDefault(f => f.Key == SetupKeys.ReleaseEnvironmentVariable);
        if (field is null)
        {
            return [];
        }

        string value = field.Field.Value.Trim();
        if (!SetupConfigSchema.IsEnvironmentVariableName(value))
        {
            throw new InvalidOperationException($"Refusing to write an invalid release environment-variable name '{value}'.");
        }

        string path = SetupAssetPaths.ResolveInside(repositoryRoot, RelativePath);
        if (!File.Exists(path))
        {
            return [];
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return [];
        }

        if (root is not JsonObject obj)
        {
            return [];
        }

        if (obj[EnvVarProperty]?.GetValue<string>() == value)
        {
            return []; // already correct — idempotent.
        }

        obj[EnvVarProperty] = value;
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        File.WriteAllText(path, obj.ToJsonString(options));
        return [RelativePath];
    }
}
