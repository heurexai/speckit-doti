using Microsoft.Extensions.Configuration;

namespace Hx.Scaffold.Core.Configuration;

public sealed class HxLocalConfiguration
{
    public const string FileName = "hx.config.json";

    public int SchemaVersion { get; set; }

    public HxLocalReleaseOutputConfiguration LocalReleaseOutput { get; set; } = new();

    public string SourcePath { get; set; } = "";

    public string Source => FileName;
}

public sealed class HxLocalReleaseOutputConfiguration
{
    public bool Enabled { get; set; } = true;

    public string? Directory { get; set; }
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

        string? releaseDirectory = configuration.LocalReleaseOutput.Directory;
        if (string.IsNullOrWhiteSpace(releaseDirectory))
        {
            throw new InvalidOperationException(
                $"hx configuration localReleaseOutput.directory is required when localReleaseOutput.enabled is true in {DisplayPath(configuration)}.");
        }

        if (!Path.IsPathRooted(releaseDirectory))
        {
            throw new InvalidOperationException(
                $"hx configuration localReleaseOutput.directory must be an absolute path: {releaseDirectory}");
        }
    }

    private static string DisplayPath(HxLocalConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration.SourcePath)
            ? HxLocalConfiguration.FileName
            : configuration.SourcePath;
}
