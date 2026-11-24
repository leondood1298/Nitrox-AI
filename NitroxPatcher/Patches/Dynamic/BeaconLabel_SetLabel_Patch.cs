using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Sends a beacon's new label from where every way of entering it ends (the game's text box calls SetLabel, and so do
///     VR keyboards that skip OnHandClick; the idea of upstream pull request 2844). See <see cref="Beacons.LabelSet" /> for
///     when it is not sent.
/// </summary>
public sealed partial class BeaconLabel_SetLabel_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BeaconLabel t) => t.SetLabel(default(string)));

    public static void Prefix(BeaconLabel __instance, out string __state)
    {
        __state = __instance.GetLabel();
    }

    public static void Postfix(BeaconLabel __instance, string __state)
    {
        Resolve<Beacons>().LabelSet(__instance, __state);
    }
}
