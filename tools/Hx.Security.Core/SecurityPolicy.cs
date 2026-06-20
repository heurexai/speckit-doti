using System.Text.Json;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Security.Core;

public sealed record SecuritySuppression(string Id, string Justification, string? Expiry);

/// <summary>The security gate policy (<c>rules/security.json</c>): the audit severity floor + suppressions.</summary>
public sealed record SecurityPolicy(
    int SchemaVersion,
    string AuditFloor,
    IReadOnlyList<SecuritySuppression> Suppressions)
{
    public const string RelativePath = "rules/security.json";

    /// <summary>Strictest safe default when the policy file is absent: floor <c>low</c>, no suppressions.</summary>
    public static SecurityPolicy Default { get; } = new(1, "low", []);
}

public static class SecurityPolicyLoader
{
    public static SecurityPolicy Load(string repositoryRoot)
    {
        RepositoryPath path = RepositoryPathResolver.ResolveInside(repositoryRoot, SecurityPolicy.RelativePath);
        if (!File.Exists(path.FullPath))
        {
            return SecurityPolicy.Default;
        }

        SecurityPolicy? policy = JsonSerializer.Deserialize<SecurityPolicy>(
            File.ReadAllText(path.FullPath), JsonContractSerializerOptions.Create());
        return policy ?? SecurityPolicy.Default;
    }
}

public static class SeverityLevels
{
    /// <summary>GitHub Advisory severity rank; an unknown severity ranks high (conservative — never under-rate).</summary>
    public static int Rank(string? severity) => (severity ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "low" => 1,
        "moderate" or "medium" => 2,
        "high" => 3,
        "critical" => 4,
        _ => 3
    };
}
