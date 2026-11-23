using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class uGUI_MapRoomScanner_UpdateAvailableTechTypes_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.UpdateAvailableTechTypes());

    public static bool Prefix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanTypes>().ShouldRunVanilla(__instance);

    public static void Postfix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanTypes>().Publish(__instance);
}
