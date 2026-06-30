using System.Text;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>029 FR-004/D7: the <c>config show</c> renderers — home in Doti.Core (beside the store; reachable by
/// Runner.Cli→Doti.Core). Builds the human table (grouped by what each setting drives, a default-vs-custom column, and
/// a <c>N custom · M default</c> footer) and the machine JSON shape (<c>{ group: { key: {value, source, default} } }</c>).
/// Pure rendering over the resolved model; no IO.</summary>
public static class SetupConfigTableFormatter
{
    /// <summary>The human table: rows grouped by <see cref="SetupGroup"/>, each marked default/custom, with the footer.</summary>
    public static string FormatHuman(ResolvedSetupConfig resolved)
    {
        var builder = new StringBuilder();
        int custom = 0;
        int defaulted = 0;

        foreach (SetupGroup group in Enum.GetValues<SetupGroup>())
        {
            IReadOnlyList<ResolvedSetupField> rows = resolved.Fields.Where(f => f.Group == group).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            builder.Append("## ").Append(group).Append('\n');
            foreach (ResolvedSetupField row in rows)
            {
                bool isCustom = row.Field.Source != ConfigSource.Default;
                if (isCustom)
                {
                    custom++;
                }
                else
                {
                    defaulted++;
                }

                string marker = isCustom ? "custom " : "default";
                builder.Append("  [").Append(marker).Append("] ")
                    .Append(row.Key).Append(" = ").Append(Display(row.Field.Value))
                    .Append("  (source: ").Append(SourceText(row.Field.Source)).Append(")\n");
            }
        }

        builder.Append('\n').Append(custom).Append(" custom · ").Append(defaulted).Append(" default\n");
        return builder.ToString();
    }

    /// <summary>The custom/default counts (for the summary line + the JSON footer).</summary>
    public static (int Custom, int Default) Counts(ResolvedSetupConfig resolved)
    {
        int custom = resolved.Fields.Count(f => f.Field.Source != ConfigSource.Default);
        return (custom, resolved.Fields.Count - custom);
    }

    /// <summary>The machine JSON projection: a group→key→{value,source,default} object the agent reads (FR-004).</summary>
    public static SetupConfigShowView ToView(ResolvedSetupConfig resolved)
    {
        var groups = new Dictionary<string, IReadOnlyDictionary<string, ConfigField>>(StringComparer.Ordinal);
        foreach (SetupGroup group in Enum.GetValues<SetupGroup>())
        {
            var keys = new Dictionary<string, ConfigField>(StringComparer.Ordinal);
            foreach (ResolvedSetupField field in resolved.Fields.Where(f => f.Group == group))
            {
                keys[field.Key] = field.Field;
            }

            if (keys.Count > 0)
            {
                groups[CamelCase(group.ToString())] = keys;
            }
        }

        (int custom, int defaulted) = Counts(resolved);
        return new SetupConfigShowView(groups, custom, defaulted);
    }

    private static string SourceText(ConfigSource source) => source switch
    {
        ConfigSource.ConfigFile => "config-file",
        ConfigSource.Interactive => "interactive",
        ConfigSource.Flag => "flag",
        ConfigSource.Derived => "derived",
        _ => "default",
    };

    private static string Display(string value) => value.Length == 0 ? "(none)" : value;

    private static string CamelCase(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
}

/// <summary>029 FR-004: the <c>config show --json</c> payload — grouped <c>{value, source, default}</c> per key + counts.</summary>
public sealed record SetupConfigShowView(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ConfigField>> Groups,
    int Custom,
    int Default);
