using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game has one time capsule for all rockets. Opened from a rocket's cockpit, it shows that rocket's capsule.
/// </summary>
public sealed partial class TimeCapsuleHandScanner_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((TimeCapsuleHandScanner t) => t.OnHandClick(default));

    public static void Prefix(TimeCapsuleHandScanner __instance)
    {
        Resolve<TimeCapsules>().ShowCapsuleOfRocket(__instance);
    }
}
