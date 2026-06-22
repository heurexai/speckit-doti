namespace Hx.Cli.Kernel;

/// <summary>Identifies the tool emitting the envelope (the Identity ring's tool + version).</summary>
public sealed record CliMeta(string Tool, string Version);
