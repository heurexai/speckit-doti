using Hx.Cycle.Core;
using Hx.Cycle.Core.Tasks;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class DotiTaskCompletionTests
{
    [Fact]
    public void ValidateFeature_FailsClosed_ForMissingTaskFile()
    {
        string dir = NewTempDir();
        try
        {
            TaskCompletionResult result = DotiTaskCompletion.ValidateFeature(dir, "001-example");

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            TaskCompletionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("missing-task-file", diagnostic.Reason);
            Assert.Equal("docs/tasks/001-example-tasks.md", diagnostic.Path);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateFeature_Fails_ForUncheckedTask_AndCheckedTaskMissingHash()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example",
                "- [ ] `T001` (FR-001, SC-001) - Finish the first task.\n" +
                "- [x] `T002` (FR-002, SC-002) - Finish the second task.\n");

            TaskCompletionResult result = DotiTaskCompletion.ValidateFeature(dir, "001-example");

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            Assert.Contains(result.Diagnostics, d => d.TaskId == "T001" && d.Reason == "unchecked");
            Assert.Contains(result.Diagnostics, d => d.TaskId == "T002" && d.Reason == "missing-hash");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateFeature_Fails_ForDuplicateTaskIds()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example",
                "- [x] `T001` (FR-001, SC-001) - Finish the first task.\n" +
                "- [x] `T001` (FR-002, SC-002) - Finish the duplicate task.\n");

            TaskCompletionResult result = DotiTaskCompletion.ValidateFeature(dir, "001-example");

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            Assert.Contains(result.Diagnostics, d => d.TaskId == "T001" && d.Reason == "duplicate-task-id");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Hash_IgnoresWhitespaceLineEndingsCheckboxAndMarker()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example", "- [x] `T001`   (FR-001,   SC-001)   - Finish   the task.\r\n");
            DotiTaskRecord original = Assert.Single(DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example"));
            string hash = DotiTaskCompletion.ComputeHash(original);

            WriteTasks(dir, "001-example",
                "- [ ] `T001` (FR-001, SC-001) - Finish the task. <!-- doti-task-hash: " + hash + " -->\n");
            DotiTaskRecord reformatted = Assert.Single(DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example"));

            Assert.Equal(hash, DotiTaskCompletion.ComputeHash(reformatted));
            Assert.Equal(StageOutcome.Fail, DotiTaskCompletion.ValidateFeature(dir, "001-example").Outcome);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateFeature_PassesCheckedHash_AndFailsAfterMeaningfulChange()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example", "- [x] `T001` (FR-001, SC-001) - Finish the task.\n");
            DotiTaskRecord task = Assert.Single(DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example"));
            string hash = DotiTaskCompletion.ComputeHash(task);

            WriteTasks(dir, "001-example",
                "- [x] `T001` (FR-001, SC-001) - Finish the task. <!-- doti-task-hash: " + hash + " -->\n");
            Assert.Equal(StageOutcome.Pass, DotiTaskCompletion.ValidateFeature(dir, "001-example").Outcome);

            WriteTasks(dir, "001-example",
                "- [x] `T001` (FR-001, SC-999) - Finish the changed task. <!-- doti-task-hash: " + hash + " -->\n");
            TaskCompletionResult changed = DotiTaskCompletion.ValidateFeature(dir, "001-example");

            Assert.Equal(StageOutcome.Fail, changed.Outcome);
            Assert.Contains(changed.Diagnostics, d => d.TaskId == "T001" && d.Reason == "hash-mismatch");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateActiveFeature_UsesCycleStateFeature()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example", "- [ ] `T001` (FR-001, SC-001) - Finish the task.\n");
            new CycleStateStore(dir).Write(new CycleState(1, "001-example", "HEAD", "implement", []));

            TaskCompletionResult result = DotiTaskCompletion.ValidateActiveFeature(dir);

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            Assert.Equal("001-example", result.Feature);
            Assert.Contains(result.Diagnostics, d => d.TaskId == "T001" && d.Reason == "unchecked");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StampFeature_StampsCheckedTasks_AndStillFailsForUncheckedTasks()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example",
                "- [x] `T001` (FR-001, SC-001) - Finish the first task.\n" +
                "- [ ] `T002` (FR-002, SC-002) - Finish the second task.\n");

            TaskHashStampResult result = DotiTaskCompletion.StampFeature(dir, "001-example");

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            Assert.Equal(2, result.EvaluatedCount);
            Assert.Equal(1, result.UpdatedCount);
            Assert.Contains(result.Diagnostics, d => d.TaskId == "T002" && d.Reason == "unchecked");
            string text = File.ReadAllText(Path.Combine(dir, "docs", "tasks", "001-example-tasks.md"));
            Assert.Equal(1, CountOccurrences(text, DotiTaskCompletion.HashMarkerName));
            Assert.Contains("T001", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StampFeature_WritesHashesForCheckedTasks_ThatValidate()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example",
                "- [x] `T001` (FR-001, SC-001) - Finish the first task.\n" +
                "- [X] `T002` (FR-002, SC-002) - Finish the second task.\n");

            TaskHashStampResult stamp = DotiTaskCompletion.StampFeature(dir, "001-example");

            Assert.Equal(StageOutcome.Pass, stamp.Outcome);
            Assert.Equal(2, stamp.UpdatedCount);
            string text = File.ReadAllText(Path.Combine(dir, "docs", "tasks", "001-example-tasks.md"));
            Assert.Equal(2, CountOccurrences(text, DotiTaskCompletion.HashMarkerName));
            Assert.Equal(StageOutcome.Pass, DotiTaskCompletion.ValidateFeature(dir, "001-example").Outcome);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CreateProof_IncludesRecomputableTaskSetHash_Items_AndDiagnostics()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example",
                "- [x] `T001` (FR-001, SC-001) - Finish the first task.\n" +
                "- [ ] `T002` (FR-002, SC-002) - Finish the second task.\n");

            TaskCompletionProof proof = DotiTaskCompletion.CreateProof(dir, "001-example");
            IReadOnlyList<DotiTaskRecord> tasks = DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example");

            Assert.Equal(StageOutcome.Fail, proof.Outcome);
            Assert.Equal("001-example", proof.Feature);
            Assert.Equal("docs/tasks/001-example-tasks.md", proof.TaskFile);
            Assert.Equal(2, proof.TaskCount);
            Assert.Equal(DotiTaskCompletion.ComputeTaskSetHash(tasks), proof.TaskSetHash);
            Assert.Collection(proof.Tasks,
                t =>
                {
                    Assert.Equal("T001", t.TaskId);
                    Assert.True(t.Checked);
                    Assert.Equal(DotiTaskCompletion.ComputeHash(tasks[0]), t.CanonicalHash);
                },
                t =>
                {
                    Assert.Equal("T002", t.TaskId);
                    Assert.False(t.Checked);
                    Assert.Equal(DotiTaskCompletion.ComputeHash(tasks[1]), t.CanonicalHash);
                });
            Assert.Contains(proof.Diagnostics, d => d.TaskId == "T002" && d.Reason == "unchecked");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-task-completion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteTasks(string repositoryRoot, string feature, string content)
    {
        string dir = Path.Combine(repositoryRoot, "docs", "tasks");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, feature + "-tasks.md"), content);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int start = 0;
        while ((start = haystack.IndexOf(needle, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += needle.Length;
        }

        return count;
    }
}
