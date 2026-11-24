using System.Reflection;
using NitroxClient.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The local player's pain sounds are played through emitters other games do not have; they are sent as one-shots
///     (see <see cref="PlayerPainSounds" />). A prefix, so that "not already playing" is read before the game starts the
///     sound.
/// </summary>
public sealed partial class PlayerDamageSounds_OnTakeDamage_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PlayerDamageSounds t) => t.OnTakeDamage(default(DamageInfo)));

    public static void Prefix(PlayerDamageSounds __instance, DamageInfo damageInfo)
    {
        Resolve<PlayerPainSounds>().BeforePain(__instance, damageInfo);
    }
}
