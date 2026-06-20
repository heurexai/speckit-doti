using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hx.Cycle.Core;

/// <summary>One declared cycle stage from <c>workflow.yml</c> (schemaVersion 2): its id, the command that
/// runs it, its <c>kind</c> (doc | review | diff | commit), the artifact it <c>produces</c> (a path
/// pattern with <c>{feature}</c>, or null for stages with no file yet), and its prerequisite stage ids.</summary>
public sealed record CycleStage(string Id, string Command, string Kind, string? Produces, IReadOnlyList<string> Prereqs);

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

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        WorkflowDoc? doc = deserializer.Deserialize<WorkflowDoc>(File.ReadAllText(workflowYmlPath));
        if (doc is null)
        {
            throw new InvalidOperationException($"Workflow file is empty: {workflowYmlPath}");
        }

        if (doc.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported workflow schemaVersion {doc.SchemaVersion} at {workflowYmlPath}; this runner requires {SupportedSchemaVersion}. Re-install the doti assets.");
        }

        if (doc.Stages is null || doc.Stages.Count == 0)
        {
            throw new InvalidOperationException($"Workflow declares no stages: {workflowYmlPath}");
        }

        var stages = new List<CycleStage>(doc.Stages.Count);
        foreach (StageEntry entry in doc.Stages)
        {
            string id = entry.Id ?? throw new InvalidOperationException($"A stage is missing 'id' in {workflowYmlPath}.");
            stages.Add(new CycleStage(
                id,
                string.IsNullOrWhiteSpace(entry.Command) ? id : entry.Command!,
                string.IsNullOrWhiteSpace(entry.Kind) ? "diff" : entry.Kind!,
                string.IsNullOrWhiteSpace(entry.Produces) ? null : entry.Produces,
                entry.Prereqs ?? []));
        }

        return new StageModel(stages);
    }

    public CycleStage Find(string stageId) =>
        Stages.FirstOrDefault(s => string.Equals(s.Id, stageId, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Unknown stage '{stageId}'. Known stages: {string.Join(", ", Stages.Select(s => s.Id))}.");

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
    }
}
