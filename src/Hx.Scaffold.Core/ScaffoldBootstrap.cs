using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

public static class ScaffoldBootstrap
{
    public static ScaffoldProfile DefaultProfile { get; } = new("dotnet-cli", "hx-dotnet-cli", "net10.0");

    public static ScaffoldRequest CreateRequest(string name, string company, string outputPath)
    {
        return new ScaffoldRequest(name, company, outputPath, DefaultProfile.Name, ["codex", "claude"]);
    }
}
