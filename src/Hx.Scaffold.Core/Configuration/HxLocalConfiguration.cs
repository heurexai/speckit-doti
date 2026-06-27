using Hx.Scaffold.Core.Release;
using Microsoft.Extensions.Configuration;

namespace Hx.Scaffold.Core.Configuration;

public sealed class HxLocalConfiguration
{
    public const string FileName = "hx.config.json";

    public int SchemaVersion { get; set; }

    public HxLocalReleaseOutputConfiguration LocalReleaseOutput { get; set; } = new();

    /// <summary>
    /// 008 FR-041: the local LLM model root for the advisory semantic drift finder. Optional — when set it WINS over the
    /// <c>HEUREX_LLM_ROOT</c> environment variable; when neither is provided the finder has no engine and skips
    /// (advisory only, never gating).
    /// </summary>
    public string? LlmModelRoot { get; set; }

    public string SourcePath { get; set; } = "";

    public string Source => FileName;
}

public sealed class HxLocalReleaseOutputConfiguration
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// An explicit, absolute local release root. OPTIONAL — when set it WINS over any environment variable (so a
    /// machine that wants a fixed path just sets it). When omitted, the root resolves from the environment variable
    /// (<see cref="EnvironmentVariable"/>, else the release-target manifest's default, else <c>DOTI_RELEASE_ROOT</c>);
    /// if that is also unset, the local copy is skipped (the tag + package proofs still run). NEVER hard-code a
    /// machine path in a committed config — leave this null and resolve through the environment.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>
    /// OPTIONAL machine-local override of the environment variable NAME used to resolve the release root (e.g.
    /// <c>HEUREX_RELEASE_ROOT</c> on a specific PC). When null the release-target manifest's
    /// <c>defaultReleaseRootEnvironmentVariable</c> applies, defaulting to <c>DOTI_RELEASE_ROOT</c>. Ignored when
    /// <see cref="Directory"/> is set (an explicit root always wins).
    /// </summary>
    public string? EnvironmentVariable { get; set; }
}

public static class HxLocalConfigurationLoader
{
    public static HxLocalConfiguration LoadRequired(string executableDirectory)
    {
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            throw new InvalidOperationException("hx configuration directory is required.");
        }

        string directory = Path.GetFullPath(executableDirectory);
        string path = Path.Combine(directory, HxLocalConfiguration.FileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Required hx configuration file was not found next to hx.exe: {path}");
        }

        IConfigurationRoot root = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(HxLocalConfiguration.FileName, optional: false, reloadOnChange: false)
            .Build();

        var configuration = new HxLocalConfiguration();
        root.Bind(configuration);
        configuration.SourcePath = path;
        Validate(configuration);
        return configuration;
    }

    public static void Validate(HxLocalConfiguration configuration)
    {
        if (configuration.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"hx configuration schemaVersion must be 1 in {DisplayPath(configuration)}.");
        }

        configuration.LocalReleaseOutput ??= new HxLocalReleaseOutputConfiguration();
        if (!configuration.LocalReleaseOutput.Enabled)
        {
            return;
        }

        // The directory is OPTIONAL: when omitted the root resolves from the environment (so a committed config carries
        // no machine path). When present it must be absolute.
        string? releaseDirectory = configuration.LocalReleaseOutput.Directory;
        if (!string.IsNullOrWhiteSpace(releaseDirectory) && !Path.IsPathRooted(releaseDirectory))
        {
            throw new InvalidOperationException(
                $"hx configuration localReleaseOutput.directory must be an absolute path: {releaseDirectory}");
        }

        string? environmentVariable = configuration.LocalReleaseOutput.EnvironmentVariable;
        if (!string.IsNullOrWhiteSpace(environmentVariable)
            && !LocalReleaseRootResolver.IsValidEnvironmentVariableName(environmentVariable))
        {
            throw new InvalidOperationException(
                $"hx configuration localReleaseOutput.environmentVariable '{environmentVariable}' is not a valid environment variable name in {DisplayPath(configuration)}.");
        }
    }

    private static string DisplayPath(HxLocalConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration.SourcePath)
            ? HxLocalConfiguration.FileName
            : configuration.SourcePath;
}
