using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Fire_Douse_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Fire t) => t.Douse(default(float)));

    public static void Postfix(Fire __instance, float amount)
    {
        if (!__instance.livemixin.IsAlive() || __instance.IsExtinguished())
        {
            Resolve<Fires>().OnDouse(__instance, Fires.EXTINGUISH_DOUSE_AMOUNT);

            // A Cyclops fire went out. The player simulating the Cyclops updates its stored state so that players who
            // join later do not get the fire back. This also runs when another player's dousing is applied here.
            if (__instance.fireSubRoot && __instance.fireSubRoot.TryGetNitroxId(out NitroxId cyclopsId) &&
                Resolve<SimulationOwnership>().HasAnyLockType(cyclopsId))
            {
                Resolve<Cyclops>().BroadcastMetadataChange(cyclopsId);
            }
        }
        else
        {
            Resolve<Fires>().OnDouse(__instance, amount);
        }
    }
}
