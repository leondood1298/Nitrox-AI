using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Every PowerSource checks its relay once a second (PowerSource.Start's InvokeRepeating). A Lifepod cell has no
///     update of its own like a solar panel; this is where the player simulating it sends the level its own draws left,
///     the heartbeat, and the first state of a cell the server has none for
///     (see <see cref="BasePowerSources.BroadcastIfSimulatedCell" />).
/// </summary>
public sealed partial class PowerSource_UpdateConnectionCallback_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PowerSource t) => t.UpdateConnectionCallback());

    public static void Postfix(PowerSource __instance) => Resolve<BasePowerSources>().BroadcastIfSimulatedCell(__instance);
}
