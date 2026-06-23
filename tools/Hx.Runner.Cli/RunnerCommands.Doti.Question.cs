using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    private static readonly JsonSerializerOptions QuestionJsonOptions = JsonContractSerializerOptions.Create();

    public static CliResult QuestionCheck(CliMeta meta, string file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return Usage(meta, "doti question check", "--file is required.");
        }

        OperatorQuestion? question;
        try
        {
            question = JsonSerializer.Deserialize<OperatorQuestion>(File.ReadAllText(file), QuestionJsonOptions);
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
}
