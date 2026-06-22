using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Impact.Core.Planning;

public static class AffectedTestProofHasher
{
    public static string HashPlan(AffectedPlan plan)
    {
        var canonical = new
        {
            plan.SchemaVersion,
            plan.Outcome,
            AffectedSourceProjects = plan.AffectedSourceProjects.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
            SelectedTests = plan.SelectedTests
                .OrderBy(t => Normalize(t.ProjectPath), StringComparer.OrdinalIgnoreCase)
                .Select(t => new { t.TestProject, ProjectPath = Normalize(t.ProjectPath), t.Command })
                .ToArray(),
            Reasons = plan.Reasons.OrderBy(r => r, StringComparer.Ordinal).ToArray(),
        };
        return HashJson(canonical);
    }

    public static string HashTestScope(IEnumerable<string> projectPaths) =>
        HashJson(projectPaths.Select(Normalize).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray());

    public static string HashExecutedTests(IEnumerable<ExecutedTestProject> executed)
    {
        var canonical = executed
            .OrderBy(t => Normalize(t.ProjectPath), StringComparer.OrdinalIgnoreCase)
            .Select(t => new
            {
                t.TestProject,
                ProjectPath = Normalize(t.ProjectPath),
                t.Command,
                t.ExitCode,
                Outcome = t.Outcome.ToString(),
            })
            .ToArray();
        return HashJson(canonical);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string HashJson<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, JsonContractSerializerOptions.Create());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
