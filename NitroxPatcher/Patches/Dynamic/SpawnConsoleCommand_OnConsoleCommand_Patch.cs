using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents local player from using "spawn" command without the rank the console needs (<see cref="ConsoleCommandPolicy.REQUIRED_PERMS" />).
/// </summary>
public sealed partial class SpawnConsoleCommand_OnConsoleCommand_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((SpawnConsoleCommand t) => t.OnConsoleCommand_spawn(default));

    public static bool Prefix(NotificationCenter.Notification n)
    {
        if (!ConsoleCommandPolicy.MaySubmit(Resolve<LocalPlayer>().Permissions))
        {
            Log.InGame(Language.main.Get("Nitrox_MissingPermission").Replace("{PERMISSION}", ConsoleCommandPolicy.REQUIRED_PERMS.ToString()));
            return false;
        }
        return true;
    }
}
