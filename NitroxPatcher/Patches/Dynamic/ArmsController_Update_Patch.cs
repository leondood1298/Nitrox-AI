using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class ArmsController_Update_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((ArmsController t) => t.Update());

    /// <summary>
    ///     Another player's body only keeps its IK up to date: everything else ArmsController.Update does reads the local
    ///     player. Its IK is set up again, as the game does on a change of tool, PDA or world target, with that player's
    ///     own tool so that a two-handed tool is held in both hands.
    /// </summary>
    public static bool Prefix(ArmsController __instance)
    {
        if (__instance.smoothSpeedAboveWater == 0)
        {
            PlayerTool? tool = RemoteArms.TryGetRemotePlayer(__instance, out RemotePlayer remotePlayer) ? remotePlayer.HeldTool : null;
            if (__instance.reconfigureWorldTarget || (tool && tool.PollForceConfigureIK()))
            {
                __instance.Reconfigure(tool);
                __instance.reconfigureWorldTarget = false;
            }

            __instance.leftAim.Update(__instance.ikToggleTime);
            __instance.rightAim.Update(__instance.ikToggleTime);
            __instance.UpdateHandIKWeights();
            return false;
        }

        return true;
    }
}
