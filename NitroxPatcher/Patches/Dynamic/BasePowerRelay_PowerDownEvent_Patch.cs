using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Skips the "base out of power" voice callback while <see cref="BasePowerRelayAudioSuppressor" /> says the relay is
///     still being reconciled.
/// </summary>
public sealed partial class BasePowerRelay_PowerDownEvent_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BasePowerRelay t) => t.PowerDownEvent(default));

    public static bool Prefix(BasePowerRelay __instance) => Resolve<BasePowerRelayAudioSuppressor>().ShouldPlay(__instance, "audio_down");
}
