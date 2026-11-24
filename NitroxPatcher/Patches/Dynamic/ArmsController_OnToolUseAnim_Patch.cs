using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The tool-use animation event of another player's body (a knife swing, a Dive Reel throw) would use the local
///     player's tool: their knife would strike what the local player looks at. It only animates that player's own tool.
/// </summary>
public sealed partial class ArmsController_OnToolUseAnim_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ArmsController t) => t.OnToolUseAnim(default));

    public static bool Prefix(ArmsController __instance)
    {
        if (!RemoteArms.IsRemote(__instance))
        {
            return true;
        }
        if (RemoteArms.TryGetRemotePlayer(__instance, out RemotePlayer remotePlayer))
        {
            remotePlayer.OnToolUseAnimation();
        }
        return false;
    }
}
