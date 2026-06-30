namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 D3: the outcome of a projection — the relative paths written + the keys reported as ignored
/// (new-only keys reached on the install path, or projectable keys with no injected writer).</summary>
public sealed record SetupProjectionResult(
    IReadOnlyList<string> Written,
    IReadOnlyList<SetupIgnoredKey> Ignored)
{
    public static readonly SetupProjectionResult Empty = new([], []);
}

/// <summary>One key the projector did not write, with the reason (so nothing is silently dropped — FR-002).</summary>
public sealed record SetupIgnoredKey(string Key, string Reason);

/// <summary>
/// 029 FR-002/FR-006/D3/D10: the PURE projection orchestration. Iterates the resolved key→target table and dispatches
/// each custom (operator-supplied) field to the injected <see cref="ISetupTargetWriter"/> for its target. Lives in
/// Contracts (IO-free); the concrete writers live at their asset, so no <c>Doti.Core→Scaffold.Core</c> edge is ever
/// required. <b>Provable no-op fence (D10):</b> when <paramref name="resolved"/> is <c>null</c> — or carries no custom
/// projectable field — ZERO writers are called and no file is touched (SC-007 byte-identical on both surfaces).
/// </summary>
public static class SetupConfigProjector
{
    public static SetupProjectionResult Project(
        ResolvedSetupConfig? resolved,
        string repositoryRoot,
        IReadOnlyDictionary<SetupTarget, ISetupTargetWriter> writers)
    {
        // D10 no-op fence: absent config → return before touching any writer.
        if (resolved is null)
        {
            return SetupProjectionResult.Empty;
        }

        var written = new List<string>();
        var ignored = new List<SetupIgnoredKey>();

        // Group the CUSTOM, projectable fields by their target (a default-sourced field is never projected).
        var byTarget = new Dictionary<SetupTarget, List<ResolvedSetupField>>();
        foreach (ResolvedSetupField field in resolved.Fields)
        {
            if (field.Field.Source == ConfigSource.Default)
            {
                continue; // unchanged — never written (idempotent; preserves the template default).
            }

            // A new-only key resolved on the install audience is reported, not written (FR-002).
            if (resolved.Audience == SetupAudience.Install && field.Audience == SetupAudience.New)
            {
                ignored.Add(new SetupIgnoredKey(field.Key, "new-only field is not applicable to `hx doti install`."));
                continue;
            }

            if (field.Target is SetupTarget.None or SetupTarget.TemplateToken or SetupTarget.AgentSet)
            {
                continue; // not post-projected here (template tokens applied at generation; agents via install metadata; None is informational).
            }

            if (!byTarget.TryGetValue(field.Target, out List<ResolvedSetupField>? list))
            {
                list = [];
                byTarget[field.Target] = list;
            }

            list.Add(field);
        }

        foreach ((SetupTarget target, List<ResolvedSetupField> fields) in byTarget)
        {
            if (!writers.TryGetValue(target, out ISetupTargetWriter? writer))
            {
                foreach (ResolvedSetupField field in fields)
                {
                    ignored.Add(new SetupIgnoredKey(field.Key, $"no writer is wired for target '{target}'."));
                }

                continue;
            }

            written.AddRange(writer.Write(repositoryRoot, fields));
        }

        return new SetupProjectionResult(written, ignored);
    }
}
