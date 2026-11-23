using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Lets <see cref="BasePowerRelayAudioSuppressor" /> remember relays that are created during initial sync.
/// </summary>
public sealed partial class BasePowerRelay_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BasePowerRelay t) => t.Start());

    public static void Postfix(BasePowerRelay __instance)
    {
        Resolve<BasePowerRelayAudioSuppressor>().TrackIfLoading(__instance);
    }
}
