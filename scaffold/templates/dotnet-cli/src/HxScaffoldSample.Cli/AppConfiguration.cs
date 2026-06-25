using Microsoft.Extensions.Configuration;

namespace HxScaffoldSample.Cli;

public sealed class AppConfiguration
{
    public const string FileName = "HxScaffoldSample.Cli.config.json";

    public int SchemaVersion { get; set; }

    public GreetingConfiguration Greeting { get; set; } = new();

    public string SourcePath { get; set; } = "";

    public static AppConfiguration LoadRequired(string executableDirectory)
    {
        string directory = Path.GetFullPath(executableDirectory);
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Required application configuration file was not found next to the executable: {path}");
        }

        IConfigurationRoot root = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(FileName, optional: false, reloadOnChange: false)
            .Build();

        var configuration = new AppConfiguration();
        root.Bind(configuration);
        configuration.SourcePath = path;
        configuration.Validate();
        return configuration;
    }

    private void Validate()
    {
        if (SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Application configuration schemaVersion must be 1 in {SourcePath}.");
        }

        if (string.IsNullOrWhiteSpace(Greeting.Prefix))
        {
            throw new InvalidOperationException($"Application configuration greeting.prefix is required in {SourcePath}.");
        }
    }
}

public sealed class GreetingConfiguration
{
    public string Prefix { get; set; } = "Hello";
}
