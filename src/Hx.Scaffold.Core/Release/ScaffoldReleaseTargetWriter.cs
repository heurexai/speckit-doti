namespace Hx.Scaffold.Core.Release;

internal static class ScaffoldReleaseTargetWriter
{
    public static void WriteDefault(string targetRoot, string projectName)
    {
        string cliProjectName = projectName + ".Cli";
        ReleaseTargetManifest.WriteDefault(
            targetRoot,
            productName: projectName,
            publishProject: $"src/{cliProjectName}/{cliProjectName}.csproj",
            publishedExecutableName: cliProjectName,
            executableName: projectName,
            defaultReleaseRootEnvironmentVariable: LocalReleaseRootResolver.DefaultEnvironmentVariableName);
    }
}
