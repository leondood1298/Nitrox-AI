using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Tells other players what the propulsion cannon in the local player's hand holds, for its beam and the glow around
///     the object (<see cref="NitroxClient.MonoBehaviours.RemotePlayerToolLook" />). The Prawn Suit's propulsion arm is
///     not a cannon in the hand and is not told here.
/// </summary>
public sealed partial class PropulsionCannon_grabbedObject_set_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Property((PropulsionCannon t) => t.grabbedObject).SetMethod;

    public static void Prefix(PropulsionCannon __instance, out GameObject __state)
    {
        __state = __instance._grabbedObject;
    }

    public static void Postfix(PropulsionCannon __instance, GameObject __state)
    {
        GameObject grabbed = __instance._grabbedObject;
        if (grabbed == __state || !Inventory.main || Inventory.main.GetHeldTool() is not PropulsionCannonWeapon weapon || weapon.propulsionCannon != __instance)
        {
            return;
        }
        LocalPlayer localPlayer = Resolve<LocalPlayer>();
        if (!localPlayer.SessionId.HasValue)
        {
            return;
        }
        NitroxId? grabbedId = grabbed && grabbed.TryGetNitroxId(out NitroxId id) ? id : null;
        Resolve<IPacketSender>().Send(new PlayerPropulsionGrab(localPlayer.SessionId.Value, grabbedId));
    }
}
