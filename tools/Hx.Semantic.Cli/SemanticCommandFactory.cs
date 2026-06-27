using System.CommandLine;
using Hx.Cli.Kernel;
using Hx.Embedding;
using Hx.Semantic;
using Hx.Tooling.Contracts;

namespace Hx.Semantic.Cli;

/// <summary>The dev-only semantic CLI surface (M-5): a single advisory <c>drift-candidates</c> command. The shipped
/// operator surface is <c>hx doti drift-candidates</c>, composed in the packed tool over the same Hx.Semantic.Core.</summary>
public static class SemanticCommandFactory
{
    public static RootCommand Create(CliMeta meta)
    {
        RootCommand root = new("scaffold-dotnet advisory semantic drift finder (dev-only).");

        Command command = new("drift-candidates", "Advisory semantic drift candidates over the change set (never gating).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> baseRef = new("--base") { Description = "Base ref for the change set.", DefaultValueFactory = _ => "HEAD" };
        Option<string> modelRoot = new("--model-root") { Description = "Local LLM model root (else HEUREX_LLM_ROOT).", DefaultValueFactory = _ => "" };
        Option<double> threshold = new("--threshold") { Description = "Cosine threshold (else the engine default).", DefaultValueFactory = _ => 0 };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(baseRef);
        command.Options.Add(modelRoot);
        command.Options.Add(threshold);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "drift-candidates",
            () => DriftCandidates(meta, parseResult.GetValue(repo)!, parseResult.GetValue(baseRef)!,
                parseResult.GetValue(modelRoot)!, parseResult.GetValue(threshold)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        root.Subcommands.Add(command);

        CliApp.AddDescribe(root, meta, ErrorCodes.All);
        return root;
    }

    public static CliResult DriftCandidates(CliMeta meta, string repo, string baseRef, string modelRoot, double threshold)
    {
        try
        {
            DriftCandidatesResult result = DriftCandidateRunner.Run(
                repo,
                string.IsNullOrWhiteSpace(baseRef) ? "HEAD" : baseRef,
                string.IsNullOrWhiteSpace(modelRoot) ? null : modelRoot,
                threshold > 0 ? threshold : null);
            return CliResults.Ok(meta, "drift-candidates",
                $"{result.Candidates.Count} advisory candidate(s) via {result.ActiveEngine} ({result.ChunksEmbedded} chunks embedded).",
                result);
        }
        catch (SemanticException ex)
        {
            // No provisioned engine / model — advisory only, so a SKIP (exit 0), never a failure.
            return CliResults.Skipped(meta, "drift-candidates", $"semantic finder skipped (advisory): {ex.Message}");
        }
    }
}
