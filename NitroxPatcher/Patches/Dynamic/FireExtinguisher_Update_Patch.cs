using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FireExtinguisher_Update_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((FireExtinguisher t) => t.Update());

    public static bool Prefix(FireExtinguisher __instance, out float __state)
    {
        // The fuel before this frame, to report what the frame spent (see FireExtinguishers.FuelChanged)
        __state = __instance.fuel;
        if (__instance.transform.TryGetComponentInAscendance(12, out RemotePlayerIdentifier identifier))
        {
            Resolve<FireExtinguishers>().RemoteUpdate(__instance, identifier);
            return false;
        }

        return true;
    }

    public static void Postfix(FireExtinguisher __instance, bool __runOriginal, float __state)
    {
        if (__runOriginal && __instance.TryGetNitroxId(out NitroxId id))
        {
            FireExtinguishers fireExtinguishers = Resolve<FireExtinguishers>();
            fireExtinguishers.BroadcastLocalState(id, __instance);
            fireExtinguishers.FuelChanged(__instance, id, __state);
        }
    }
}
