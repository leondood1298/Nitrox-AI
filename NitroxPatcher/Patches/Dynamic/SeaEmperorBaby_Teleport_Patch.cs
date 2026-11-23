using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Sea Emperor baby that leaves through the portal destroys itself. Every player's copy swims there and leaves, so
///     only the player simulating it tells the server, which then removes it for the players whose copy is still swimming.
/// </summary>
public sealed partial class SeaEmperorBaby_Teleport_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SeaEmperorBaby t) => t.Teleport());

    public static void Prefix(SeaEmperorBaby __instance)
    {
        // Babies made only for a hatching animation have no id
        if (__instance.TryGetNitroxId(out NitroxId babyId) && Resolve<SimulationOwnership>().HasAnyLockType(babyId))
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(babyId));
        }
    }
}
