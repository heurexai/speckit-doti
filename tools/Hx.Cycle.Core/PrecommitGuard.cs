namespace Hx.Cycle.Core;

/// <summary>
/// The .NET decision logic behind the insurance pre-commit hook. The hook is a thin,
/// untracked, logic-free stub; the deciding logic is here. A commit is sanctioned only when
/// Doti-owned transition/release code set the sentinel env var on the <c>git commit</c> subprocess; a
/// bare <c>git commit</c> has no sentinel and is routed back to the workflow. Insurance, not a hard gate
/// (a deliberate bypass that sets the sentinel still surfaces as drift on the next <c>cycle status</c>/<c>check</c>).
/// </summary>
public static class PrecommitGuard
{
    public const string SentinelEnvVar = "DOTI_SANCTIONED_COMMIT";

    public static bool IsSanctioned() =>
        string.Equals(Environment.GetEnvironmentVariable(SentinelEnvVar), "1", StringComparison.Ordinal);

    public static string RedirectMessage =>
        "This repository commits through Doti-owned workflow transitions. Continue with the next "
        + "numbered Doti workflow step instead of a bare `git commit`.";
}
