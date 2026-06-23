using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    private static readonly JsonSerializerOptions JsonOptions = JsonContractSerializerOptions.Create();

    // ---- doti render / install ----

    public static CliResult DotiRenderSkills(CliMeta meta, string repo, string agentsCsv, bool check)
    {
        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti render-skills", error!);
        }

        DotiRenderResult result = DotiRenderer.Render(repo, agents, check);
        string summary = result.Outcome == StageOutcome.Pass
            ? (check ? "No skill drift." : "Skills rendered.")
            : "Skill drift: " + string.Join(", ", result.Drifted);
        return CliResults.FromStage(meta, "doti render-skills", result.Outcome, summary, result);
    }

    public static CliResult DotiInstall(CliMeta meta, string targetRepo, string agentsCsv)
    {
        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti install", error!);
        }

        string target = Path.GetFullPath(targetRepo);
        string? source = FindDotiSource(Directory.GetCurrentDirectory());
        if (source is null)
        {
            return Usage(meta, "doti install", "Could not locate doti/core/skills.json above the current directory.");
        }

        string repoName = Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        DotiInstallResult result = DotiInstaller.Install(source, target, agents, repoName);
        return CliResults.FromStage(meta, "doti install", result.Outcome, $"Doti install into {target}.", result);
    }

    // ---- doti question check (Layers B+C) ----

    public static CliResult QuestionCheck(CliMeta meta, string file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return Usage(meta, "doti question check", "--file is required.");
        }

        OperatorQuestion? question;
        try
        {
            question = JsonSerializer.Deserialize<OperatorQuestion>(File.ReadAllText(file), JsonOptions);
        }
        catch (Exception ex)
        {
            return CliResults.Fail(meta, "doti question check", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, $"Could not read/parse the question file: {ex.Message}", target: file)]);
        }

        if (question is null)
        {
            return CliResults.Fail(meta, "doti question check", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, "The file is empty or not a JSON object.", target: file)]);
        }

        OperatorQuestionValidation validation = OperatorQuestionValidator.Validate(question);
        if (validation.Valid)
        {
            return CliResults.Ok(meta, "doti question check", "The operator question is valid.", validation);
        }

        List<Diagnostic> errors = validation.Errors.Select(e => Diag.Of(ErrorCodes.Validation_Failed, e)).ToList();
        return CliResults.Fail(meta, "doti question check", ExitClass.Validation, errors,
            "The operator question violates the protocol.", validation);
    }

    private static bool TryParseAgents(string csv, out List<DotiAgentTarget> agents, out string? error)
    {
        agents = [];
        foreach (string key in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            DotiAgentTarget? agent = DotiAgentTarget.FromKey(key);
            if (agent is null)
            {
                error = $"Unknown agent '{key}'. Known: codex, claude.";
                return false;
            }

            agents.Add(agent);
        }

        if (agents.Count == 0)
        {
            agents.AddRange(DotiAgentTarget.All);
        }

        error = null;
        return true;
    }

    private static string? FindDotiSource(string start)
    {
        DirectoryInfo? dir = new(Path.GetFullPath(start));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "doti", "core", "skills.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
