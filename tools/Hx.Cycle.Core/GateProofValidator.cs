using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Graph;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public static class GateProofValidator
{
    public static IReadOnlyList<string> ValidateAffectedTestProof(string repositoryRoot, PersistedGateProof persisted)
    {
        AffectedTestProof? proof = persisted.Proof.AffectedTestProof;
        if (proof is null)
        {
            return ["gate proof has no affected-test proof; re-run `gate run` with the current runner"];
        }

        var reasons = new List<string>();
        if (proof.SchemaVersion != JsonContractDefaults.SchemaVersion)
        {
            reasons.Add($"affected-test proof schema version {proof.SchemaVersion} is unsupported");
        }

        if (!string.Equals(proof.BaseRef, persisted.BaseRef, StringComparison.Ordinal))
        {
            reasons.Add("affected-test proof base ref does not match the persisted gate proof");
        }

        if (!string.Equals(proof.HeadRef, "HEAD", StringComparison.Ordinal))
        {
            reasons.Add("affected-test proof head ref must be HEAD");
        }

        if (proof.ExecutedTests.Any(t => t.ExitCode != 0 || t.Outcome != StageOutcome.Pass))
        {
            reasons.Add("affected-test proof contains a failed test execution");
        }

        string selfPlanHash = AffectedTestProofHasher.HashPlan(proof.Plan);
        if (!string.Equals(selfPlanHash, proof.PlanHash, StringComparison.Ordinal))
        {
            reasons.Add("affected-test proof plan hash does not match the embedded plan");
        }

        string selfExecutedHash = AffectedTestProofHasher.HashExecutedTests(proof.ExecutedTests);
        if (!string.Equals(selfExecutedHash, proof.ExecutedTestsHash, StringComparison.Ordinal))
        {
            reasons.Add("affected-test proof execution hash does not match the embedded executions");
        }

        try
        {
            AffectedPlan expectedPlan = new AffectedTestPlanner().Plan(
                repositoryRoot, persisted.BaseRef, proof.HeadRef, proof.Configuration);
            string expectedPlanHash = AffectedTestProofHasher.HashPlan(expectedPlan);
            if (!string.Equals(expectedPlanHash, proof.PlanHash, StringComparison.Ordinal))
            {
                reasons.Add("affected-test proof is stale or forged: planner hash does not match the current change set");
            }

            bool expectedFullSuite = persisted.Lane == Lane.Release || expectedPlan.Outcome == AffectedOutcome.FullGateRequired;
            if (proof.FullSuite != expectedFullSuite)
            {
                reasons.Add(expectedFullSuite
                    ? "affected-test proof did not record the required full test suite"
                    : "affected-test proof unexpectedly records a full-suite run for a narrowed plan");
            }

            IReadOnlyList<string> expectedProjects = expectedFullSuite
                ? AllTestProjectPaths(repositoryRoot)
                : expectedPlan.SelectedTests.Select(t => t.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string expectedScopeHash = AffectedTestProofHasher.HashTestScope(expectedProjects);
            if (!string.Equals(expectedScopeHash, proof.TestScopeHash, StringComparison.Ordinal))
            {
                reasons.Add("affected-test proof selected test-scope hash does not match the expected planner scope");
            }

            string[] executedProjects = proof.ExecutedTests
                .Select(t => Normalize(t.ProjectPath))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] normalizedExpected = expectedProjects
                .Select(Normalize)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!executedProjects.SequenceEqual(normalizedExpected, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("affected-test proof executions do not match the expected test project set");
            }
        }
        catch (Exception ex)
        {
            reasons.Add("could not recompute affected-test proof: " + ex.Message);
        }

        return reasons;
    }

    private static IReadOnlyList<string> AllTestProjectPaths(string repositoryRoot)
    {
        string[] solutions = Directory.GetFiles(repositoryRoot, "*.slnx");
        if (solutions.Length != 1)
        {
            return [];
        }

        ProjectGraph graph = new ProjectGraphBuilder().Build(repositoryRoot, Path.GetFileName(solutions[0]));
        return graph.Nodes.Values
            .Where(n => n.IsTestProject)
            .Select(n => n.Path)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
