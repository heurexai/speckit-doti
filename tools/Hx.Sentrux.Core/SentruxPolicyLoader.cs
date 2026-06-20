using System.Text.Json;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static class SentruxPolicyLoader
{
    public const string RelativePath = "rules/sentrux.json";

    /// <summary>
    /// Loads `rules/sentrux.json`. Fails closed (throws) when present but invalid;
    /// returns the default policy when absent so early-phase repos still behave,
    /// with <paramref name="usedDefault"/> set so callers can record it.
    /// </summary>
    public static SentruxPolicy Load(string repositoryRoot, out bool usedDefault)
    {
        RepositoryPath policyPath = RepositoryPathResolver.ResolveInside(repositoryRoot, RelativePath);
        if (!File.Exists(policyPath.FullPath))
        {
            usedDefault = true;
            return SentruxPolicy.Default();
        }

        SentruxPolicy policy = JsonSerializer.Deserialize<SentruxPolicy>(
                File.ReadAllText(policyPath.FullPath), JsonContractSerializerOptions.Create())
            ?? throw new InvalidOperationException($"Sentrux policy is empty or invalid: {RelativePath}");

        usedDefault = false;
        return policy;
    }
}
