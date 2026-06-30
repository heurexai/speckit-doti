using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>029 FR-002/D5/D9: the outcome of loading + validating a <c>--config</c> file. <see cref="Ok"/> only when
/// <see cref="Errors"/> is empty and <see cref="Config"/> is bound. Carries the validation errors (each naming the
/// offending field) so the CLI renders them without re-validating.</summary>
public sealed record SetupConfigLoadResult(SetupConfig? Config, IReadOnlyList<SetupValidationError> Errors)
{
    public bool Ok => Errors.Count == 0 && Config is not null;

    public static SetupConfigLoadResult Failed(string field, string message) =>
        new(null, [new SetupValidationError(field, message)]);
}

/// <summary>
/// 029 FR-002/D5/D9: reads + schema-validates a <c>--config</c> file and contains its path inside the working tree.
/// Lives in Doti.Core (IO + the pure <see cref="SetupConfigSchema"/>) so BOTH CLI hosts (<c>hx new</c>,
/// <c>hx doti install</c>) and the wizard share ONE load+validate path; validation runs in the CLI BEFORE the request
/// is built, so an invalid config never reaches generation (SC-006). The CLI layers the output-path containment on top.
/// </summary>
public static class SetupConfigLoader
{
    /// <summary>Read + validate <paramref name="configPath"/>; the path is contained inside <paramref name="baseDirectory"/>
    /// (reject <c>..</c> escape). Fail-closed on a missing file, traversal, malformed JSON, or any invalid value.</summary>
    public static SetupConfigLoadResult Load(string configPath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return SetupConfigLoadResult.Failed("--config", "a --config path is required.");
        }

        string baseFull = Path.GetFullPath(baseDirectory);
        string full = Path.GetFullPath(Path.Combine(baseFull, configPath));
        string baseRoot = baseFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!full.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full, baseFull, StringComparison.OrdinalIgnoreCase))
        {
            return SetupConfigLoadResult.Failed("--config", $"config path escapes the working tree: {configPath}");
        }

        if (!File.Exists(full))
        {
            return SetupConfigLoadResult.Failed("--config", $"config file not found: {configPath}");
        }

        string json;
        try
        {
            json = File.ReadAllText(full);
        }
        catch (IOException ex)
        {
            return SetupConfigLoadResult.Failed("--config", $"could not read config file: {ex.Message}");
        }

        SetupValidationResult validation = SetupConfigSchema.ValidateRaw(json, out SetupConfig? config);
        return validation.Ok && config is not null
            ? new SetupConfigLoadResult(config, [])
            : new SetupConfigLoadResult(null, validation.Errors);
    }
}
