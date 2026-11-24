using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Beacon.Start sets the label the beacon already has, which is not a rename; and a beacon lying in the world, thrown by
///     another player or before this player joined, starts humming here (see <see cref="Beacons" />).
/// </summary>
public sealed partial class Beacon_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Beacon t) => t.Start());

    public static void Prefix()
    {
        Resolve<Beacons>().StartBegins();
    }

    public static void Postfix(Beacon __instance)
    {
        Beacons beacons = Resolve<Beacons>();
        beacons.StartEnds();
        beacons.Started(__instance);
    }
}
