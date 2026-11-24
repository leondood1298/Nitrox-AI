using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An egg in an Alien Containment hatches in the game of the player who simulates the containment: that game makes
///     the creature and removes the egg for everybody. The other games wait for it, so that an egg never vanishes in one
///     game while no creature comes out of it.
/// </summary>
public sealed partial class CreatureEgg_Hatch_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((CreatureEgg t) => t.Hatch());

    public static bool Prefix(CreatureEgg __instance)
    {
        // This case always destroys the creature egg (see original code)
        if (!__instance.TryGetComponent(out WaterParkItem waterParkItem))
        {
            return true;
        }

        // We don't manage eggs with no id here
        if (!__instance.TryGetNitroxId(out NitroxId eggId))
        {
            return true;
        }

        // It is VERY IMPORTANT to check for simulation ownership on the water park and not on the egg
        // since that's the convention we chose
        bool containmentHasId = waterParkItem.currentWaterPark.TryGetNitroxId(out NitroxId waterParkId);
        bool simulatesContainment = containmentHasId && Resolve<SimulationOwnership>().HasAnyLockType(waterParkId);
        if (!ContainmentRules.DecidesHere(containmentHasId, simulatesContainment))
        {
            return false;
        }
        if (containmentHasId)
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(eggId));
        }
        return true;
    }
}
