using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCamera_HandleInput_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.HandleInput());

    public static void Postfix(MapRoomCamera __instance)
    {
        MapRoomCameras cameras = Resolve<MapRoomCameras>();
        cameras.BroadcastLightIfChanged(__instance);
        cameras.BroadcastComponentStateIfChanged(__instance);
    }
}
