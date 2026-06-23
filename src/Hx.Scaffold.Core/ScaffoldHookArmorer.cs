using Hx.Cycle.Core;

namespace Hx.Scaffold.Core;

internal static class ScaffoldHookArmorer
{
    public static void Arm(string targetRoot, Action<string, string> emit)
    {
        emit("doti-hook", "running");
        DotiHookInstallResult hook = HookInstaller.InstallIfSafe(targetRoot);
        if (!hook.Success)
        {
            throw new InvalidOperationException(hook.Message);
        }

        emit("doti-hook", hook.Changed ? "pass" : hook.Action);
    }
}
