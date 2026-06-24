using System.Text.RegularExpressions;

namespace Hx.Cycle.Core;

public sealed class CycleInputException : Exception
{
    public CycleInputException(string message)
        : base(message)
    {
    }
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
