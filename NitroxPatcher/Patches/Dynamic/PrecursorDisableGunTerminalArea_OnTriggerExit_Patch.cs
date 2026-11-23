using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Lets the trigger react to other players too, and keeps it open while anybody is still inside.
/// </summary>
public sealed partial class PrecursorDisableGunTerminalArea_OnTriggerExit_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorDisableGunTerminalArea t) => t.OnTriggerExit(default));

    public static bool Prefix(PrecursorDisableGunTerminalArea __instance, Collider other)
    {
        return Resolve<PrecursorProximityTriggers>().GunTerminalAreaExited(__instance, other);
    }
}