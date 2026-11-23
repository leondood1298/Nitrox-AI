using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The terminal on the ion cube pillar near the incubator takes the crystal at the click but powers the incubator only
///     when the player's cinematic ends. The crystal's removal and the incubator's state are both sent then, so that a
///     player who leaves during the cinematic leaves nothing behind.
/// </summary>
public sealed partial class IncubatorActivationTerminal_OnPlayerCinematicModeEnd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((IncubatorActivationTerminal t) => t.OnPlayerCinematicModeEnd(default(PlayerCinematicController)));

    public static void Prefix(IncubatorActivationTerminal __instance)
    {
        if (__instance.crystalObject.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(id));
        }
    }

    public static void Postfix(IncubatorActivationTerminal __instance)
    {
        // The server only knows about the main incubator platform which is the direct parent
        GameObject platform = __instance.transform.parent.gameObject;
        if (__instance.incubator.powered && platform.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Entities>().BroadcastMetadataUpdate(id, new IncubatorMetadata(true, __instance.incubator.hatched));
        }
    }
}
