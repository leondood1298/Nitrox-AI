using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcasts simulated Sea Dragons exosuit grabbing.
/// </summary>
public sealed partial class SeaDragon_GrabExosuit_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((SeaDragon t) => t.GrabExosuit(default));

    public static void Prefix(SeaDragon __instance, Exosuit exosuit)
    {
        if (__instance.TryGetNitroxId(out NitroxId seaDragonId) && Resolve<SimulationOwnership>().HasAnyLockType(seaDragonId) &&
            exosuit.TryGetNitroxId(out NitroxId targetId))
        {
            Resolve<IPacketSender>().Send(new SeaDragonGrabExosuit(seaDragonId, targetId));
        }
    }
}
