using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hx.Cycle.Core;

/// <summary>One declared cycle stage from <c>workflow.yml</c> (schemaVersion 2): its id, the command that
/// runs it, its <c>kind</c> (doc | review | diff | release), the artifact it <c>produces</c> (a path
/// pattern with <c>{feature}</c>, or null for stages with no file yet), its prerequisite stage ids, and (028 FR-007)
/// its declared successor stage ids (<c>next</c>) — the forward edges the action model projects stage-advance
/// affordances from. Additive trailing within schemaVersion 2; empty for the terminal stage.</summary>
public sealed record CycleStage(
    string Id, string Command, string Kind, string? Produces, IReadOnlyList<string> Prereqs, IReadOnlyList<string> Next);

/// <summary>
/// The cycle stage model, read from the installed <c>.doti/workflows/doti/workflow.yml</c>. Fails closed
/// on an unrecognized <c>schemaVersion</c> (never mis-reads a v1 file as v2) and on an empty/invalid
/// stage list. The stage model is declarative + single-sourced in <c>workflow.yml</c>.
/// </summary>
public sealed class StageModel
{
    public const int SupportedSchemaVersion = 2;

    public IReadOnlyList<CycleStage> Stages { get; }

    private StageModel(IReadOnlyList<CycleStage> stages) => Stages = stages;

    public static StageModel Load(string workflowYmlPath)
    {
        if (!File.Exists(workflowYmlPath))
        {
            throw new FileNotFoundException($"Workflow file not found: {workflowYmlPath}");
        }

        WorkflowDoc doc = ReadWorkflow(workflowYmlPath);
        ValidateWorkflow(doc, workflowYmlPath);
        return new StageModel(doc.Stages!.Select(entry => ToCycleStage(entry, workflowYmlPath)).ToArray());
    }

    public CycleStage Find(string stageId) =>
        Stages.FirstOrDefault(s => string.Equals(s.Id, stageId, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Unknown stage '{stageId}'. Known stages: {string.Join(", ", Stages.Select(s => s.Id))}.");

    /// <summary>The transitive prerequisite closure of a stage, in workflow declaration order (deterministic).</summary>
    public IReadOnlyList<CycleStage> TransitivePrereqStages(string stageId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>(Find(stageId).Prereqs);
        while (stack.Count > 0)
        {
            string id = stack.Pop();
            if (!seen.Add(id))
            {
                continue;
            }

            foreach (string parent in Find(id).Prereqs)
            {
                stack.Push(parent);
            }
        }

        return Stages.Where(s => seen.Contains(s.Id)).ToList();
    }

    /// <summary>Resolve a stage's <c>produces</c> path pattern for a feature (substitutes <c>{feature}</c>).
    /// Lives on the dependency-leaf <see cref="StageModel"/> so both <see cref="FreshnessEvaluator"/> and
    /// <see cref="CanonicalArtifactHasher"/> can use it without a type cycle.</summary>
    public static string ResolveProduces(string producesPattern, string feature) =>
        producesPattern.Replace("{feature}", feature);

    // YAML binding shapes (private; unmatched properties — name/maturity/rules/etc. — are ignored).
    private sealed class WorkflowDoc
    {
        public int SchemaVersion { get; set; }
        public List<StageEntry>? Stages { get; set; }
    }

    private sealed class StageEntry
    {
        public string? Id { get; set; }
        public string? Command { get; set; }
        public string? Kind { get; set; }
        public string? Produces { get; set; }
        public List<string>? Prereqs { get; set; }
        public List<string>? Next { get; set; }
    }

    private static WorkflowDoc ReadWorkflow(string workflowYmlPath)
    {
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<WorkflowDoc>(File.ReadAllText(workflowYmlPath))
            ?? throw new InvalidOperationException($"Workflow file is empty: {workflowYmlPath}");
    }

    private static void ValidateWorkflow(WorkflowDoc doc, string workflowYmlPath)
    {
        if (doc.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported workflow schemaVersion {doc.SchemaVersion} at {workflowYmlPath}; this runner requires {SupportedSchemaVersion}. Re-install the doti assets.");
        }

        if (doc.Stages is null || doc.Stages.Count == 0)
        {
            throw new InvalidOperationException($"Workflow declares no stages: {workflowYmlPath}");
        }
    }

    private static CycleStage ToCycleStage(StageEntry entry, string workflowYmlPath)
    {
        string id = entry.Id ?? throw new InvalidOperationException($"A stage is missing 'id' in {workflowYmlPath}.");
        return new CycleStage(
            id,
            string.IsNullOrWhiteSpace(entry.Command) ? id : entry.Command!,
            string.IsNullOrWhiteSpace(entry.Kind) ? "diff" : entry.Kind!,
            string.IsNullOrWhiteSpace(entry.Produces) ? null : entry.Produces,
            entry.Prereqs ?? [],
            entry.Next ?? []);
    }
}
