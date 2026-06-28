using System.Text;

namespace Hx.Tooling.Contracts;

/// <summary>
/// 014 (FR-001/005): the SHARED emit/parse contract for ArchUnitNET violation detail. The architecture test
/// assembly EMITS one marker block per failing rule into its assertion-failure message; the runner
/// (<c>ArchitectureTestRunner.ParseTrx</c>) PARSES those blocks back out of the TRX failure message. Keeping both
/// sides on this one type is what stops the emit format and the parse format from drifting.
///
/// <para>Format (single line, deterministic, survives TRX XML text — no XML-special delimiters):
/// <c>##HXARCHVIOLATION## rule={rule} ||desc={description} ||objects={o1};{o2};... ##END##</c>. The delimiter tokens
/// and the object separator are escaped inside the payload so a rule/description/object that itself contains them
/// round-trips. A failure message may carry zero or more blocks; <see cref="Parse"/> returns them in document order.
/// </para>
/// </summary>
public static class ArchitectureViolationMarker
{
    public const string Start = "##HXARCHVIOLATION##";
    public const string End = "##END##";
    public const string RulePrefix = " rule=";
    public const string DescDelimiter = " ||desc=";
    public const string ObjectsDelimiter = " ||objects=";
    public const string ObjectSeparator = ";";

    // Escaping: the four delimiter substrings (the object separator + the three field markers) plus the End token and
    // a backslash are percent-style escaped so a payload that literally contains one cannot break the single-line
    // block. Deterministic and reversible.
    private static readonly (string Literal, string Escaped)[] Escapes =
    [
        ("\\", "\\5c"),
        (";", "\\3b"),
        (" ||", "\\7c"),
        ("=", "\\3d"),
        ("#", "\\23"),
    ];

    /// <summary>Emit one single-line marker block for a failing rule. Empty <paramref name="violatingObjects"/> is
    /// valid (a rule that reported a description but no per-object granularity).</summary>
    public static string Format(string rule, string description, IReadOnlyList<string> violatingObjects)
    {
        string objects = string.Join(ObjectSeparator, violatingObjects.Select(Escape));
        return $"{Start}{RulePrefix}{Escape(rule)}{DescDelimiter}{Escape(description)}{ObjectsDelimiter}{objects}{End}";
    }

    /// <summary>Extract zero-or-more violation blocks from a TRX failure message, in document order. A malformed
    /// block is skipped (the runner treats a failing test with no parseable block as fail-closed unknown).</summary>
    public static IReadOnlyList<ArchitectureViolation> Parse(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return [];
        }

        var violations = new List<ArchitectureViolation>();
        int cursor = 0;
        while (true)
        {
            int start = message.IndexOf(Start, cursor, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            int end = message.IndexOf(End, start, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            string block = message[(start + Start.Length)..end];
            cursor = end + End.Length;
            if (TryParseBlock(block, out ArchitectureViolation? violation))
            {
                violations.Add(violation!);
            }
        }

        return violations;
    }

    private static bool TryParseBlock(string block, out ArchitectureViolation? violation)
    {
        violation = null;
        if (!block.StartsWith(RulePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        int descAt = block.IndexOf(DescDelimiter, StringComparison.Ordinal);
        int objectsAt = block.IndexOf(ObjectsDelimiter, StringComparison.Ordinal);
        if (descAt < 0 || objectsAt < 0 || objectsAt < descAt)
        {
            return false;
        }

        string rule = Unescape(block[RulePrefix.Length..descAt]);
        string description = Unescape(block[(descAt + DescDelimiter.Length)..objectsAt]);
        string objectsRaw = block[(objectsAt + ObjectsDelimiter.Length)..];
        IReadOnlyList<string> objects = objectsRaw.Length == 0
            ? []
            : objectsRaw.Split(ObjectSeparator).Select(Unescape).ToArray();

        violation = new ArchitectureViolation(rule, description, objects);
        return true;
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value);
        foreach ((string literal, string escaped) in Escapes)
        {
            builder.Replace(literal, escaped);
        }

        return builder.ToString();
    }

    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value);
        // Reverse order so the backslash escape (introduced first) is restored last — no double-decode.
        for (int i = Escapes.Length - 1; i >= 0; i--)
        {
            builder.Replace(Escapes[i].Escaped, Escapes[i].Literal);
        }

        return builder.ToString();
    }
}
