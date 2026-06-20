using System.Text.Json;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Hygiene;

public static class HygienePolicyLoader
{
    public const string RelativePath = "rules/hygiene.json";

    /// <summary>
    /// Loads <c>rules/hygiene.json</c>. Fails closed (throws) when the file is
    /// present but invalid. Returns the default policy when the file is absent
    /// so early-phase repos still scan; callers can record that as an advisory.
    /// </summary>
    public static HygienePolicy Load(string repositoryRoot, out bool usedDefault)
    {
        RepositoryPath policyPath = RepositoryPathResolver.ResolveInside(repositoryRoot, RelativePath);
        if (!File.Exists(policyPath.FullPath))
        {
            usedDefault = true;
            return HygienePolicy.Default();
        }

        string json = File.ReadAllText(policyPath.FullPath);
        HygienePolicy? policy = JsonSerializer.Deserialize<HygienePolicy>(json, JsonContractSerializerOptions.Create())
            ?? throw new InvalidOperationException($"Hygiene policy is empty or invalid: {RelativePath}");

        usedDefault = false;
        return policy;
    }
}
