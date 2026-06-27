using Hx.Gate.Core;
using Hx.Impact.Core.ChangeDetection;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Gate.Tests;

/// <summary>
/// 012 (T008): the <see cref="GateTraceProjector"/> assembles the operator-facing trace from a finished proof,
/// honors the two-tier rule (basic always, classes + inventory only at implement-stage code), and computes the
/// effective EXECUTION mode distinctly from the planner outcome (release/escalation force full).
/// </summary>
public sealed class GateTraceTests
{
    private sealed class FakeNumstat(params NumstatEntry[] entries) : INumstatReader
    {
        public IReadOnlyList<NumstatEntry> Read(string repositoryRoot, string baseRef, string headRef) => entries;
    }

    private static GateProof PassProof(GateScope scope, params GateStep[] steps) =>
        new(JsonContractDefaults.SchemaVersion, StageOutcome.Pass, steps, [], Scope: scope);

    private static ChangeSetContext Context(params ChangedFile[] files) =>
        new(JsonContractDefaults.SchemaVersion, "base", "HEAD", "sha", true, true, null, files, []);

    private static AffectedPlan AffectedPlanWith(params string[] testProjects)
    {
        SelectedTest[] selected = testProjects
            .Select(p => new SelectedTest(System.IO.Path.GetFileNameWithoutExtension(p), p, $"dotnet test {p}"))
            .ToArray();
        return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.Affected, [], selected, []);
    }

    [Fact]
    public void EffectiveMode_release_forces_full_even_for_a_partial_plan()
    {
        AffectedPlan plan = AffectedPlanWith("test/A.Tests/A.Tests.csproj");
        Assert.Equal(GateEffectiveMode.Full, GateTraceProjector.EffectiveMode(plan, Lane.Release));
    }

    [Fact]
    public void EffectiveMode_full_gate_required_is_full()
    {
        var plan = new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.FullGateRequired, [], [], ["broad"]);
        Assert.Equal(GateEffectiveMode.Full, GateTraceProjector.EffectiveMode(plan, Lane.Normal));
    }

    [Fact]
    public void EffectiveMode_affected_with_selection_is_partial()
    {
        AffectedPlan plan = AffectedPlanWith("test/A.Tests/A.Tests.csproj");
        Assert.Equal(GateEffectiveMode.Partial, GateTraceProjector.EffectiveMode(plan, Lane.Normal));
    }

    [Fact]
    public void EffectiveMode_no_tests_required_is_none()
    {
        var plan = new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.NoTestsRequired, [], [], []);
        Assert.Equal(GateEffectiveMode.None, GateTraceProjector.EffectiveMode(plan, Lane.Normal));
    }

    [Fact]
    public void Assemble_carries_scope_steps_and_total_from_the_proof()
    {
        var scope = new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []);
        GateStep step = new("hygiene", StageOutcome.Pass, [new GateEvidence("h", "ok")], DurationMs: 12);
        GateProof proof = PassProof(scope, step);
        var change = new ChangeSummary(1, 0, 0, 0, 5, 1, ["src/a.cs"], [], false);

        GateTrace trace = GateTraceProjector.Assemble(proof, change, null, AffectedPlanWith("test/A.Tests/A.Tests.csproj"), Lane.Normal, 99);

        Assert.Equal(scope, trace.Scope);
        Assert.Same(proof.Steps, trace.Steps);
        Assert.Equal(99, trace.TotalMs);
        Assert.Equal(GateEffectiveMode.Partial, trace.EffectiveMode);
        Assert.False(trace.Change.ClassesIncluded);
        Assert.Null(trace.Tests);
    }

    [Fact]
    public void Project_docs_only_change_builds_basic_summary_only_even_when_implement_stage()
    {
        using var repo = new TempRepo();
        var scope = new GateScope(JsonContractDefaults.SchemaVersion, true, "docs-only", ["architecture-test", "sentrux-check"]);
        GateProof proof = PassProof(scope, new GateStep("affected-change", StageOutcome.Pass, [], 1));
        ChangeSetContext change = Context(new ChangedFile("docs/readme.md", ChangeStatus.Modified, null));
        var plan = new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.NoTestsRequired, [], [], []);
        var projector = new GateTraceProjector(new FakeNumstat(new NumstatEntry(3, 0, "docs/readme.md")));

        // implementStageCode=true, but a docs-only change has no code → basic tier only.
        GateTrace trace = projector.Project(repo.Path, proof, change, plan, Lane.Normal, "base", "HEAD", "Release",
            implementStageCode: true, totalMs: 10);

        Assert.False(trace.Change.ClassesIncluded);
        Assert.Empty(trace.Change.ClassesTouched);
        Assert.Null(trace.Tests);
        Assert.Equal(1, trace.Change.Docs);
        Assert.Equal(GateEffectiveMode.None, trace.EffectiveMode);
    }

    [Fact]
    public void Project_non_implement_code_change_stays_basic_tier()
    {
        using var repo = new TempRepo();
        repo.Write("src/A/Widget.cs", "namespace A; public class Widget { }");
        var scope = new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []);
        GateProof proof = PassProof(scope, new GateStep("affected-change", StageOutcome.Pass, [], 1));
        ChangeSetContext change = Context(new ChangedFile("src/A/Widget.cs", ChangeStatus.Modified, null));
        AffectedPlan plan = AffectedPlanWith("test/A.Tests/A.Tests.csproj");
        var projector = new GateTraceProjector(new FakeNumstat(new NumstatEntry(4, 0, "src/A/Widget.cs")));

        // A code change but NOT the implement stage → no classes, no inventory (FR-021).
        GateTrace trace = projector.Project(repo.Path, proof, change, plan, Lane.Normal, "base", "HEAD", "Release",
            implementStageCode: false, totalMs: 10);

        Assert.False(trace.Change.ClassesIncluded);
        Assert.Empty(trace.Change.ClassesTouched);
        Assert.Null(trace.Tests);
        Assert.Equal(1, trace.Change.Source);
    }

    [Fact]
    public void Project_implement_code_change_adds_classes_and_inventory()
    {
        using var repo = new TempRepo();
        repo.Write("src/A/Widget.cs", "namespace A; public sealed class Widget { } public record Gadget;");
        var scope = new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []);
        GateProof proof = PassProof(scope, new GateStep("affected-change", StageOutcome.Pass, [], 1));
        ChangeSetContext change = Context(new ChangedFile("src/A/Widget.cs", ChangeStatus.Modified, null));
        AffectedPlan plan = AffectedPlanWith("test/A.Tests/A.Tests.csproj");
        var projector = new GateTraceProjector(new FakeNumstat(new NumstatEntry(7, 2, "src/A/Widget.cs")));

        GateTrace trace = projector.Project(repo.Path, proof, change, plan, Lane.Normal, "base", "HEAD", "Release",
            implementStageCode: true, totalMs: 42);

        Assert.True(trace.Change.ClassesIncluded);
        Assert.Equal(["Gadget", "Widget"], trace.Change.ClassesTouched);
        Assert.NotNull(trace.Tests); // inventory attempted (no .slnx in temp → honest unknown, but present)
        Assert.Equal(7, trace.Change.LinesAdded);
        Assert.Equal(2, trace.Change.LinesRemoved);
    }

    private sealed class TempRepo : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "hx-gate-trace-" + Guid.NewGuid().ToString("N"));

        public TempRepo() => Directory.CreateDirectory(Path);

        public void Write(string relative, string content)
        {
            string full = System.IO.Path.Combine(Path, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }
}
