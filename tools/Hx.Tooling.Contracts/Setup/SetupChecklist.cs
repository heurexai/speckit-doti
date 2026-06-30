namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 FR-007: one operator-intent checklist step `hx` does NOT perform — named, never executed.</summary>
public sealed record SetupChecklistItem(string Step, string Why, string Category);

/// <summary>
/// 029 FR-007: the resolved NuGet Trusted-Publishing parameters the checklist NAMES (owner/repo/workflow/environment) so
/// the operator-only OIDC step is actionable. Inert data — surfaced in the checklist, never used to call nuget.org.
/// </summary>
public sealed record SetupPublishIntent(
    bool Intended, string Owner, string Repo, string Workflow, string Environment, string Target)
{
    /// <summary>The publish intent with every parameter at its documented default (publish not intended).</summary>
    public static readonly SetupPublishIntent None = new(
        false, "", "", SetupConfigDefaults.PublishWorkflow, SetupConfigDefaults.PublishEnvironment, SetupConfigDefaults.PublishTarget);

    /// <summary>Project the publish intent out of a resolved setup config (the values the checklist names).</summary>
    public static SetupPublishIntent FromResolved(ResolvedSetupConfig? resolved)
    {
        if (resolved is null)
        {
            return None;
        }

        return new SetupPublishIntent(
            resolved.ValueOrDefault(SetupKeys.PublishEnabled) == "true",
            resolved.ValueOrDefault(SetupKeys.PublishOwner),
            resolved.ValueOrDefault(SetupKeys.PublishRepo),
            resolved.ValueOrDefault(SetupKeys.PublishWorkflow),
            resolved.ValueOrDefault(SetupKeys.PublishEnvironment),
            resolved.ValueOrDefault(SetupKeys.PublishTarget));
    }
}

/// <summary>
/// 029 FR-007/D3: the operator-intent checklist — every remaining setup step `hx` cannot or does not execute. Two
/// categories: <c>operator-only</c> (the GitHub/nuget.org trust-boundary steps `hx` must never run) and
/// <c>deferred-030</c> (the git/CI automation deferred to feature 030). PURE data, surfaced by the host commands as
/// next-actions; the publish-related items appear only when publish intent is set. `hx` prints this — it never acts on it.
/// </summary>
public static class SetupChecklist
{
    public const string CategoryOperatorOnly = "operator-only";
    public const string CategoryDeferred030 = "deferred-030";

    /// <summary>Build the checklist; the NuGet OIDC items are included only when publish is intended, and they NAME the
    /// resolved owner/repo/workflow/environment so the operator-only step is actionable (FR-007).</summary>
    public static IReadOnlyList<SetupChecklistItem> Build(SetupPublishIntent publish)
    {
        var items = new List<SetupChecklistItem>();

        if (publish.Intended)
        {
            string scope = OwnerRepoText(publish);
            items.Add(new SetupChecklistItem(
                $"Create the {publish.Target} Trusted-Publishing (OIDC) policy",
                $"Authorizes the release workflow to publish without an API key (owner: {Or(publish.Owner)}, repo: {Or(publish.Repo)}, workflow: {publish.Workflow}, environment: {publish.Environment}).",
                CategoryOperatorOnly));
            items.Add(new SetupChecklistItem(
                "Add the NUGET_USER repo secret", $"The nuget.org username the OIDC login uses for {scope} (no fallback API key).", CategoryOperatorOnly));
            items.Add(new SetupChecklistItem(
                $"Create the `{publish.Environment}` GitHub Environment", "Gates OIDC token issuance; its name must equal the policy's Environment.", CategoryOperatorOnly));
        }

        items.Add(new SetupChecklistItem(
            "Configure `main` branch protection", "required_linear_history OFF (dev→main is a merge commit), plus required status checks.", CategoryOperatorOnly));
        items.Add(new SetupChecklistItem(
            "Push the `v*` release tag", "Triggers the publish workflow; `/09-doti-release` owns the push, the operator authorizes it.", CategoryOperatorOnly));

        // Git/CI automation deferred to feature 030 (C1) — surfaced here, not performed this cycle.
        items.Add(new SetupChecklistItem(
            "Make the sanctioned baseline commit", "GitVersion needs a commit and the insurance hook blocks bare commits (deferred to 030).", CategoryDeferred030));
        items.Add(new SetupChecklistItem(
            "Create the `dev` / `main` two-branch model", "The release path assumes both; a fresh git init has neither (deferred to 030).", CategoryDeferred030));
        items.Add(new SetupChecklistItem(
            "Emit `.github/workflows/{release,ci,dco}.yml`", "Tag/pack/publish + CI + DCO automation; the scaffold ships no .github/ (deferred to 030).", CategoryDeferred030));
        items.Add(new SetupChecklistItem(
            "Install the DCO prepare-commit-msg hook", "Auto-signs commits for the DCO check (deferred to 030).", CategoryDeferred030));

        return items;
    }

    /// <summary>The checklist items rendered as next-actions (the Direction ring), so the host surfaces them uniformly.</summary>
    public static IReadOnlyList<CliNextAction> AsNextActions(SetupPublishIntent publish) =>
        Build(publish)
            .Select(item => new CliNextAction($"[{item.Category}] {item.Step}", item.Why))
            .ToList();

    private static string OwnerRepoText(SetupPublishIntent publish) =>
        publish.Owner.Length > 0 && publish.Repo.Length > 0 ? $"{publish.Owner}/{publish.Repo}" : "this repo";

    private static string Or(string value) => value.Length > 0 ? value : "(unset)";
}
