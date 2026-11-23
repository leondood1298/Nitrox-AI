using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A player pressed the button on the room's screen that stops the scan. The game's other caller of this method
///     never gets here, see <see cref="uGUI_MapRoomScanner_OnDisable_Patch" />.
/// </summary>
public sealed partial class uGUI_MapRoomScanner_OnCancelScan_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.OnCancelScan());

    public static void Postfix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScans>().TargetChosen(__instance.mapRoom);
}
