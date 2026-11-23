using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcast the sub root change of a player respawning.
/// </summary>
public sealed partial class Player_MovePlayerToRespawnPoint_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Player t) => t.MovePlayerToRespawnPoint());

    public static void Postfix(Player __instance)
    {
        // A respawn in the pod has already been sent by the pod patches (EscapePod_RespawnPlayer_Patch, also for the
        // warpme command, and Player_SetCurrentEscapePod_Patch). The server keeps one sub root per player, so an empty
        // sub root sent after them would take the player out of the pod again.
        if (!__instance.currentSub && __instance.currentEscapePod)
        {
            return;
        }

        Optional<NitroxId> currentSubId = Optional.Empty;
        if (__instance.currentSub)
        {
            currentSubId = __instance.currentSub.GetId();
        }

        Resolve<LocalPlayer>().BroadcastSubrootChange(currentSubId);
    }
}
