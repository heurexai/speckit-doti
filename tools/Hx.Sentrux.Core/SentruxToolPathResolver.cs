namespace Hx.Sentrux.Core;

public static class SentruxToolPathResolver
{
    public static string ResolveRepoRelativeToolPath(string runtimeIdentifier)
    {
        string executableName = runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            ? "sentrux.exe"
            : "sentrux";

        return $"tools/sentrux/bin/{runtimeIdentifier}/{executableName}";
    }
}
