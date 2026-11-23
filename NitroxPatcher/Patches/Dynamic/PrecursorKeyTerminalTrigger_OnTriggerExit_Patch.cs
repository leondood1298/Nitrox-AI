using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Lets the trigger react to other players too, and keeps it open while anybody is still inside.
/// </summary>
public sealed partial class PrecursorKeyTerminalTrigger_OnTriggerExit_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorKeyTerminalTrigger t) => t.OnTriggerExit(default));

    public static bool Prefix(PrecursorKeyTerminalTrigger __instance, Collider col)
    {
        return Resolve<PrecursorProximityTriggers>().KeyTerminalTriggerExited(__instance, col);
    }
}