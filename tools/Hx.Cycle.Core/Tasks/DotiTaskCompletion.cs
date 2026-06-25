using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core.Tasks;

public static partial class DotiTaskCompletion
{
    public const string StepName = "task-completion";
    public const string EvidenceKind = "task-completion";
    public const string HashMarkerName = "doti-task-hash";

    public static TaskCompletionResult ValidateActiveFeature(string repositoryRoot)
    {
        CycleState? state = new CycleStateStore(repositoryRoot).Read();
        if (state is null || string.IsNullOrWhiteSpace(state.Feature))
        {
            return TaskCompletionResult.Skipped("No active Doti cycle state found.");
        }

        return ValidateFeature(repositoryRoot, state.Feature);
    }

    public static TaskCompletionProof CreateActiveFeatureProof(string repositoryRoot)
    {
        CycleState? state = new CycleStateStore(repositoryRoot).Read();
        if (state is null || string.IsNullOrWhiteSpace(state.Feature))
        {
            return new TaskCompletionProof(
                JsonContractDefaults.SchemaVersion,
                StageOutcome.Skipped,
                null,
                null,
                0,
                ComputeTaskSetHash([]),
                [],
                []);
        }

        return CreateProof(repositoryRoot, state.Feature);
    }

    public static TaskCompletionProof CreateProof(string repositoryRoot, string feature)
    {
        TaskCompletionResult result = ValidateFeature(repositoryRoot, feature);
        IReadOnlyList<DotiTaskRecord> tasks = result.TaskFile is null || !File.Exists(Path.Combine(repositoryRoot, result.TaskFile.Replace('/', Path.DirectorySeparatorChar)))
            ? []
            : Parse(repositoryRoot, result.TaskFile, feature);

        return new TaskCompletionProof(
            JsonContractDefaults.SchemaVersion,
            result.Outcome,
            result.Feature,
            result.TaskFile,
            tasks.Count,
            ComputeTaskSetHash(tasks),
            tasks.Select(ToProofItem).ToArray(),
            result.Diagnostics.Select(ToProofDiagnostic).ToArray());
    }

    public static TaskCompletionResult ValidateFeature(string repositoryRoot, string feature)
    {
        string relativePath = $"docs/tasks/{feature}-tasks.md";
        string fullPath = Path.Combine(repositoryRoot, "docs", "tasks", feature + "-tasks.md");
        if (!File.Exists(fullPath))
        {
            return TaskCompletionResult.Fail(
                feature,
                relativePath,
                [new TaskCompletionDiagnostic(relativePath, 0, null, "missing-task-file", "Active feature task file was not found.")]);
        }

        IReadOnlyList<DotiTaskRecord> tasks = Parse(repositoryRoot, relativePath, feature);
        var diagnostics = new List<TaskCompletionDiagnostic>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DotiTaskRecord task in tasks)
        {
            if (!seenIds.Add(task.TaskId))
            {
                diagnostics.Add(task.Diagnostic("duplicate-task-id", "Task id appears more than once in the active task file."));
                continue;
            }

            if (!task.Checked)
            {
                diagnostics.Add(task.Diagnostic("unchecked", "Required task is not checked."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(task.StoredHash))
            {
                diagnostics.Add(task.Diagnostic("missing-hash", "Checked task is missing a doti-task-hash marker."));
                continue;
            }

            string expected = ComputeHash(task);
            if (!StringComparer.OrdinalIgnoreCase.Equals(expected, task.StoredHash))
            {
                diagnostics.Add(task.Diagnostic("hash-mismatch", "Checked task hash does not match the current canonical task content."));
            }
        }

        if (tasks.Count == 0)
        {
            diagnostics.Add(new TaskCompletionDiagnostic(relativePath, 0, null, "no-tasks", "Active task file contains no required Markdown tasks."));
        }

        return diagnostics.Count == 0
            ? TaskCompletionResult.Pass(feature, relativePath, tasks.Count)
            : TaskCompletionResult.Fail(feature, relativePath, diagnostics);
    }

    public static TaskHashStampResult StampFeature(string repositoryRoot, string? feature)
    {
        string resolvedFeature = ResolveFeature(repositoryRoot, feature);
        string relativePath = $"docs/tasks/{resolvedFeature}-tasks.md";
        string fullPath = Path.Combine(repositoryRoot, "docs", "tasks", resolvedFeature + "-tasks.md");
        if (!File.Exists(fullPath))
        {
            return TaskHashStampResult.Fail(
                resolvedFeature,
                relativePath,
                0,
                0,
                0,
                [new TaskCompletionDiagnostic(relativePath, 0, null, "missing-task-file", "Active feature task file was not found.")]);
        }

        IReadOnlyList<DotiTaskRecord> tasks = Parse(repositoryRoot, relativePath, resolvedFeature);
        var diagnostics = new List<TaskCompletionDiagnostic>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DotiTaskRecord task in tasks)
        {
            if (!seenIds.Add(task.TaskId))
            {
                diagnostics.Add(task.Diagnostic("duplicate-task-id", "Task id appears more than once in the active task file."));
            }

            if (!task.Checked)
            {
                diagnostics.Add(task.Diagnostic("unchecked", "Cannot stamp a completion hash for an unchecked task."));
            }
        }

        if (tasks.Count == 0)
        {
            diagnostics.Add(new TaskCompletionDiagnostic(relativePath, 0, null, "no-tasks", "Active task file contains no required Markdown tasks."));
        }

        if (diagnostics.Any(d => d.Reason is "duplicate-task-id" or "no-tasks"))
        {
            return TaskHashStampResult.Fail(resolvedFeature, relativePath, tasks.Count, 0, 0, diagnostics);
        }

        string original = File.ReadAllText(fullPath).Replace("\r\n", "\n").Replace('\r', '\n');
        bool endsWithNewLine = original.EndsWith('\n');
        string[] lines = original.Split('\n');
        if (endsWithNewLine && lines.Length > 0 && lines[^1].Length == 0)
        {
            lines = lines[..^1];
        }

        int updated = 0;
        int unchanged = 0;
        foreach (DotiTaskRecord task in tasks.Where(t => t.Checked))
        {
            string hash = ComputeHash(task);
            int index = task.LineNumber - 1;
            string current = lines[index];
            string stamped = HashMarkerRegex().IsMatch(current)
                ? HashMarkerRegex().Replace(current, $"<!-- {HashMarkerName}: {hash} -->")
                : current.TrimEnd() + $" <!-- {HashMarkerName}: {hash} -->";
            if (StringComparer.Ordinal.Equals(current, stamped))
            {
                unchanged++;
            }
            else
            {
                lines[index] = stamped;
                updated++;
            }
        }

        File.WriteAllText(fullPath, string.Join('\n', lines) + (endsWithNewLine ? "\n" : ""));
        return diagnostics.Count == 0
            ? TaskHashStampResult.Pass(resolvedFeature, relativePath, tasks.Count, updated, unchanged)
            : TaskHashStampResult.Fail(resolvedFeature, relativePath, tasks.Count, updated, unchanged, diagnostics);
    }

    public static IReadOnlyList<DotiTaskRecord> Parse(string repositoryRoot, string relativePath, string feature)
    {
        string fullPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string[] lines = File.ReadAllText(fullPath).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var tasks = new List<DotiTaskRecord>();
        for (int i = 0; i < lines.Length; i++)
        {
            Match match = TaskLineRegex().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            string rawText = match.Groups["body"].Value;
            Match hash = HashMarkerRegex().Match(rawText);
            string? storedHash = hash.Success ? hash.Groups["hash"].Value : null;
            string canonicalText = HashMarkerRegex().Replace(rawText, "");
            tasks.Add(new DotiTaskRecord(
                feature,
                relativePath.Replace('\\', '/'),
                i + 1,
                match.Groups["checked"].Value.Equals("x", StringComparison.OrdinalIgnoreCase),
                match.Groups["id"].Value,
                canonicalText,
                storedHash));
        }

        return tasks;
    }

    public static string ComputeHash(DotiTaskRecord task)
    {
        string canonical = string.Join('\n',
            "doti-task-v1",
            NormalizeToken(task.Feature),
            NormalizePath(task.RelativePath),
            NormalizeToken(task.TaskId),
            NormalizeText(task.TextWithoutMarker));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string ComputeTaskSetHash(IReadOnlyList<DotiTaskRecord> tasks)
    {
        var lines = new List<string> { "doti-task-set-v1" };
        foreach (DotiTaskRecord task in tasks.OrderBy(t => t.LineNumber))
        {
            lines.Add(NormalizeToken(task.Feature));
            lines.Add(NormalizePath(task.RelativePath));
            lines.Add(task.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add(NormalizeToken(task.TaskId));
            lines.Add(task.Checked ? "checked" : "unchecked");
            lines.Add(ComputeHash(task));
            lines.Add(task.StoredHash?.ToLowerInvariant() ?? "");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    private static TaskCompletionProofItem ToProofItem(DotiTaskRecord task) =>
        new(task.TaskId, task.RelativePath, task.LineNumber, task.Checked, ComputeHash(task), task.StoredHash);

    private static TaskCompletionProofDiagnostic ToProofDiagnostic(TaskCompletionDiagnostic diagnostic) =>
        new(diagnostic.Path, diagnostic.LineNumber, diagnostic.TaskId, diagnostic.Reason, diagnostic.Message);

    private static string NormalizePath(string value) =>
        NormalizeToken(value.Replace('\\', '/'));

    private static string NormalizeToken(string value) =>
        WhitespaceRegex().Replace(value.Trim(), " ");

    private static string NormalizeText(string value) =>
        WhitespaceRegex().Replace(value.Trim(), " ");

    private static string ResolveFeature(string repositoryRoot, string? feature)
    {
        if (!string.IsNullOrWhiteSpace(feature))
        {
            return feature;
        }

        CycleState? state = new CycleStateStore(repositoryRoot).Read();
        if (state is null || string.IsNullOrWhiteSpace(state.Feature))
        {
            throw new InvalidOperationException("--feature is required when no active Doti cycle state exists.");
        }

        return state.Feature;
    }

    [GeneratedRegex(@"^- \[(?<checked>[ xX])\]\s+`(?<id>T[0-9A-Za-z]+)`(?<body>.*)$")]
    private static partial Regex TaskLineRegex();

    [GeneratedRegex(@"<!--\s*doti-task-hash:\s*(?<hash>[a-fA-F0-9]{64})\s*-->")]
    private static partial Regex HashMarkerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

public sealed record DotiTaskRecord(
    string Feature,
    string RelativePath,
    int LineNumber,
    bool Checked,
    string TaskId,
    string TextWithoutMarker,
    string? StoredHash)
{
    public TaskCompletionDiagnostic Diagnostic(string reason, string message) =>
        new(RelativePath, LineNumber, TaskId, reason, message);
}

public sealed record TaskCompletionDiagnostic(
    string Path,
    int LineNumber,
    string? TaskId,
    string Reason,
    string Message)
{
    public string ToEvidenceMessage()
    {
        string location = LineNumber > 0 ? $"{Path}:{LineNumber}" : Path;
        string task = string.IsNullOrWhiteSpace(TaskId) ? "" : $" {TaskId}";
        return $"{location}{task}: {Reason} - {Message}";
    }
}

public sealed record TaskCompletionResult(
    StageOutcome Outcome,
    string? Feature,
    string? TaskFile,
    int TaskCount,
    IReadOnlyList<TaskCompletionDiagnostic> Diagnostics,
    string Summary)
{
    public static TaskCompletionResult Skipped(string summary) =>
        new(StageOutcome.Skipped, null, null, 0, [], summary);

    public static TaskCompletionResult Pass(string feature, string taskFile, int taskCount) =>
        new(StageOutcome.Pass, feature, taskFile, taskCount, [], $"{taskCount} checked task(s) hash-valid for {feature}.");

    public static TaskCompletionResult Fail(string feature, string taskFile, IReadOnlyList<TaskCompletionDiagnostic> diagnostics) =>
        new(StageOutcome.Fail, feature, taskFile, 0, diagnostics, $"{diagnostics.Count} task completion issue(s) for {feature}.");
}

public sealed record TaskHashStampResult(
    StageOutcome Outcome,
    string Feature,
    string TaskFile,
    int EvaluatedCount,
    int UpdatedCount,
    int UnchangedCount,
    IReadOnlyList<TaskCompletionDiagnostic> Diagnostics,
    string Summary)
{
    public static TaskHashStampResult Pass(string feature, string taskFile, int evaluatedCount, int updatedCount, int unchangedCount) =>
        new(StageOutcome.Pass, feature, taskFile, evaluatedCount, updatedCount, unchangedCount, [], $"Stamped {updatedCount} task hash(es); {unchangedCount} already current.");

    public static TaskHashStampResult Fail(
        string feature,
        string taskFile,
        int evaluatedCount,
        int updatedCount,
        int unchangedCount,
        IReadOnlyList<TaskCompletionDiagnostic> diagnostics) =>
        new(StageOutcome.Fail, feature, taskFile, evaluatedCount, updatedCount, unchangedCount, diagnostics,
            $"Stamped {updatedCount} task hash(es); refused completion because {diagnostics.Count} issue(s) remain for {feature}.");
}
