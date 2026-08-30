using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcasts simulated Reaper Leviathan vehicle grabbing.
/// Also makes sure the random release delay is taken into account by the broadcast by overriding it.
/// </summary>
public sealed partial class ReaperLeviathan_GrabVehicle_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((ReaperLeviathan t) => t.GrabVehicle(default, default));

    public static void Postfix(ReaperLeviathan __instance, Vehicle vehicle, ReaperLeviathan.VehicleType type)
    {
        if (IsSimulatedLeviathan(__instance, out NitroxId reaperLeviathanId) && vehicle.TryGetNitroxId(out NitroxId vehicleId))
        {
            // we manually reset the release delay so that we can actually control it to broadcast it
            __instance.CancelInvoke(nameof(ReaperLeviathan.ReleaseVehicle));
            // same calculation that in the function
            float releaseDelay = 8f + UnityEngine.Random.value * 5f;
            __instance.Invoke(nameof(ReaperLeviathan.ReleaseVehicle), releaseDelay);

            double realReleaseTime = Resolve<TimeManager>().RealTimeElapsed + releaseDelay;

            Resolve<IPacketSender>().Send(new ReaperLeviathanGrabVehicle(reaperLeviathanId, vehicleId, type, realReleaseTime));
        }
    }

    public static bool IsSimulatedLeviathan(ReaperLeviathan reaperLeviathan, out NitroxId reaperLeviathanId)
    {
        return reaperLeviathan.TryGetNitroxId(out reaperLeviathanId) && Resolve<SimulationOwnership>().HasAnyLockType(reaperLeviathanId);
    }
}
