using System.Text.RegularExpressions;

namespace Hx.Cycle.Core;

public class CycleInputException : Exception
{
    public CycleInputException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 028 FR-004 / B1: thrown by the in-Stamp eligibility fence when a bare <c>doti cycle stamp</c> would clear an
/// ATTESTABLE stale (own artifact unchanged, only a prerequisite's content diverged). Distinct from the base
/// <see cref="CycleInputException"/> so the CLI routes it to <c>Validation_CycleReviewRebindRequiresAttest</c> with the
/// <c>review-rebind</c> next action, instead of the feature-slug usage error.
/// </summary>
public sealed class CycleReviewRebindRequiredException : CycleInputException
{
    public CycleReviewRebindRequiredException(string message, string target)
        : base(message) => Target = target;

    /// <summary>The stale stage that must be reviewed-rebound or re-authored.</summary>
    public string Target { get; }
}

/// <summary>028 FR-003: why a <c>doti cycle review-rebind</c> was refused.</summary>
public enum ReviewRebindRefusal
{
    /// <summary>The target stage is not stale — there is nothing to attest.</summary>
    NotStale,

    /// <summary>The target is stale but not in the attestable way (a real re-author, a review-kind verdict, a
    /// change-set-bound stage, an edge/reorder, or a binding migration) — the verb does not apply.</summary>
    Ineligible,
}

/// <summary>028 FR-003: thrown by <see cref="CycleService.ReviewRebind"/> when the target is not eligible for an
/// agent-gated reviewed-no-impact rebind. <see cref="Refusal"/> distinguishes the not-stale vs ineligible case so the
/// CLI routes to <c>Validation_CycleReviewRebindNotStale</c> / <c>Validation_CycleReviewRebindIneligible</c>.</summary>
public sealed class CycleReviewRebindIneligibleException : CycleInputException
{
    public CycleReviewRebindIneligibleException(string message, ReviewRebindRefusal refusal)
        : base(message) => Refusal = refusal;

    public ReviewRebindRefusal Refusal { get; }
}

public static partial class CycleFeatureSlug
{
    public const string FormatDescription = "NNN-short-name (for example 001-numbered-specs)";

    [GeneratedRegex("^[0-9]{3}-[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedSlugPattern();

    public static bool IsNumbered(string feature) =>
        NumberedSlugPattern().IsMatch(feature);

    public static string NumberedSlugRequiredMessage(string feature) =>
        $"Feature slug '{feature}' is not numbered. Use {FormatDescription}; choose the next three-digit prefix from existing docs/specs entries.";
}
