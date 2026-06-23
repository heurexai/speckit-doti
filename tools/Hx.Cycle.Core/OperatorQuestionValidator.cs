using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>The result of validating an <see cref="OperatorQuestion"/>: valid + the list of violations.</summary>
public sealed record OperatorQuestionValidation(bool Valid, IReadOnlyList<string> Errors);

/// <summary>
/// Fail-closed validator for the operator-question contract (Layers B+C). The SAME gate for Codex and
/// Claude: a question is valid only if every required field is present + non-empty, each option carries
/// ≥1 pro + ≥1 con + a consequence, the recommendation names a real option, confidence has a level + a
/// reason, every unverified assumption says what would verify it, and every premise cites evidence.
/// Split into per-section checks so each stays simple (and within the cyclomatic-complexity budget).
/// </summary>
public static class OperatorQuestionValidator
{
    public static OperatorQuestionValidation Validate(OperatorQuestion question)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(question.Question)) { errors.Add("question text is empty"); }
        if (string.IsNullOrWhiteSpace(question.WhyItMatters)) { errors.Add("whyItMatters is empty"); }

        ValidateOptions(question, errors);
        ValidateRecommendation(question, errors);
        ValidateConfidence(question, errors);
        ValidateAssumptions(question, errors);
        ValidatePremises(question, errors);

        return new OperatorQuestionValidation(errors.Count == 0, errors);
    }

    private static void ValidateOptions(OperatorQuestion question, List<string> errors)
    {
        if (question.Options is null || question.Options.Count < 2)
        {
            errors.Add("at least two options are required");
            return;
        }

        for (int i = 0; i < question.Options.Count; i++)
        {
            ValidateOption(question.Options[i], i, errors);
        }
    }

    private static void ValidateOption(OperatorQuestionOption option, int index, List<string> errors)
    {
        string id = string.IsNullOrWhiteSpace(option.Label) ? $"option[{index}]" : $"option '{option.Label}'";
        AddIf(errors, string.IsNullOrWhiteSpace(option.Label), $"{id}: label is empty");
        AddIf(errors, option.Pros is null || option.Pros.Count == 0, $"{id}: no pros");
        AddIf(errors, option.Cons is null || option.Cons.Count == 0, $"{id}: no cons");
        AddIf(errors, string.IsNullOrWhiteSpace(option.Consequence), $"{id}: no consequence");
    }

    private static void AddIf(List<string> errors, bool condition, string message)
    {
        if (condition)
        {
            errors.Add(message);
        }
    }

    private static void ValidateRecommendation(OperatorQuestion question, List<string> errors)
    {
        if (question.Recommendation is null || string.IsNullOrWhiteSpace(question.Recommendation.Option))
        {
            errors.Add("recommendation.option is empty");
            return;
        }

        if (question.Options is not null
            && !question.Options.Any(o => string.Equals(o.Label, question.Recommendation.Option, StringComparison.Ordinal)))
        {
            errors.Add($"recommendation '{question.Recommendation.Option}' does not name any option");
        }

        if (string.IsNullOrWhiteSpace(question.Recommendation.Reasoning))
        {
            errors.Add("recommendation.reasoning is empty");
        }
    }

    private static void ValidateConfidence(OperatorQuestion question, List<string> errors)
    {
        if (question.Confidence is null || string.IsNullOrWhiteSpace(question.Confidence.Level))
        {
            errors.Add("confidence.level is empty");
        }
        else if (string.IsNullOrWhiteSpace(question.Confidence.Reason))
        {
            errors.Add("confidence.reason is empty");
        }
    }

    private static void ValidateAssumptions(OperatorQuestion question, List<string> errors)
    {
        foreach (OperatorAssumption assumption in question.Assumptions ?? [])
        {
            if (!assumption.Verified && string.IsNullOrWhiteSpace(assumption.WhatWouldVerify))
            {
                errors.Add($"assumption '{assumption.Text}' is UNVERIFIED but does not say what would verify it");
            }
        }
    }

    private static void ValidatePremises(OperatorQuestion question, List<string> errors)
    {
        if (question.Premises is null || question.Premises.Count == 0)
        {
            errors.Add("at least one premise (with evidence) is required");
            return;
        }

        foreach (OperatorPremise premise in question.Premises)
        {
            if (string.IsNullOrWhiteSpace(premise.Evidence))
            {
                errors.Add($"premise '{premise.Claim}': no evidence");
            }
        }
    }
}
