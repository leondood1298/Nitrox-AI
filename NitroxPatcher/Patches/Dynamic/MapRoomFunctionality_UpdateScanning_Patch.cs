using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents every connected client from independently draining Scanner Room power.
/// Visual scanning and blip updates still run locally; only the simulation owner may let the drain timer elapse.
/// The owner's drain is reported to whoever simulates the power sources (see <see cref="BasePowerConsumptionForwarder" />).
/// The game calls this every frame, which is also what <see cref="MapRoomScans.Update(MapRoomFunctionality, NitroxId)" /> needs.
/// </summary>
public sealed partial class MapRoomFunctionality_UpdateScanning_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateScanning());

    public static void Prefix(MapRoomFunctionality __instance, out bool __state)
    {
        bool hasEntityId = __instance.TryGetNitroxId(out NitroxId roomId);
        bool hasOwnership = hasEntityId && Resolve<SimulationOwnership>().HasAnyLockType(roomId);
        if (hasEntityId)
        {
            Resolve<MapRoomScans>().Update(__instance, roomId);
        }
        if (ShouldSuppressPowerDrain(hasEntityId, hasOwnership))
        {
            __instance.timeLastPowerDrain = Time.time;
        }

        // A room without an id drains on every client, which must not be reported once per player.
        __state = hasOwnership;
        if (__state)
        {
            Resolve<BasePowerConsumptionForwarder>().EnterConsumer();
        }
    }

    public static void Finalizer(bool __state)
    {
        if (__state)
        {
            Resolve<BasePowerConsumptionForwarder>().ExitConsumer();
        }
    }

    internal static bool ShouldSuppressPowerDrain(bool hasEntityId, bool hasOwnership) => hasEntityId && !hasOwnership;
}
