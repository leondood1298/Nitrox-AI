using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A click opens a closed crate or takes the item from an open one. Taking the item is synced as a pickup.
/// </summary>
/// <remarks>
///     A crate that is cut open opens from <see cref="Sealed.Weld" />, whose patch sends the crate's state.
/// </remarks>
public sealed partial class SupplyCrate_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SupplyCrate t) => t.OnHandClick(default(GUIHand)));

    public static void Prefix(SupplyCrate __instance, out bool __state)
    {
        __state = __instance.open;
    }

    public static void Postfix(SupplyCrate __instance, bool __state)
    {
        if (__state || !__instance.open || !__instance.TryGetIdOrWarn(out NitroxId id))
        {
            return;
        }

        Resolve<Entities>().EntityMetadataChanged(__instance, id);
    }
}
