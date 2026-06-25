using Hx.Cycle.Core.Tasks;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class DotiTaskHashTests
{
    [Fact]
    public void Canonical_hash_ignores_whitespace_eol_checkbox_and_marker()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example", "- [x] `T001`  (FR-001,  SC-001)  - Finish   this task.\r\n");
            DotiTaskRecord original = Assert.Single(DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example"));
            string hash = DotiTaskCompletion.ComputeHash(original);

            WriteTasks(dir, "001-example",
                "- [ ] `T001` (FR-001, SC-001) - Finish this task. <!-- doti-task-hash: " + hash + " -->\n");
            DotiTaskRecord reformatted = Assert.Single(DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example"));

            Assert.Equal(hash, DotiTaskCompletion.ComputeHash(reformatted));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Canonical_hash_changes_when_task_content_changes()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example", "- [x] `T001` (FR-001, SC-001) - Finish this task.\n");
            string original = DotiTaskCompletion.ComputeHash(
                Assert.Single(DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example")));

            WriteTasks(dir, "001-example", "- [x] `T001` (FR-001, SC-002) - Finish this changed task.\n");
            string changed = DotiTaskCompletion.ComputeHash(
                Assert.Single(DotiTaskCompletion.Parse(dir, "docs/tasks/001-example-tasks.md", "001-example")));

            Assert.NotEqual(original, changed);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Proof_reports_duplicate_task_ids()
    {
        string dir = NewTempDir();
        try
        {
            WriteTasks(dir, "001-example",
                "- [x] `T001` (FR-001, SC-001) - First.\n" +
                "- [x] `T001` (FR-002, SC-002) - Duplicate.\n");

            TaskCompletionProof proof = DotiTaskCompletion.CreateProof(dir, "001-example");

            Assert.Equal(StageOutcome.Fail, proof.Outcome);
            Assert.Equal(2, proof.TaskCount);
            Assert.Contains(proof.Diagnostics, d => d.TaskId == "T001" && d.Reason == "duplicate-task-id");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-task-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteTasks(string repositoryRoot, string feature, string content)
    {
        string dir = Path.Combine(repositoryRoot, "docs", "tasks");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, feature + "-tasks.md"), content);
    }
}
