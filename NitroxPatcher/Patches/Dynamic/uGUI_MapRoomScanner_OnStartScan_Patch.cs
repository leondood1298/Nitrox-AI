using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A player picked a resource on the room's screen. This and <see cref="uGUI_MapRoomScanner_OnCancelScan_Patch" />
///     are the only places a scan target is sent from: <see cref="MapRoomFunctionality.StartScanning" /> itself is also
///     called by the game for its own reasons.
/// </summary>
public sealed partial class uGUI_MapRoomScanner_OnStartScan_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.OnStartScan(default));

    public static void Postfix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScans>().TargetChosen(__instance.mapRoom);
}
