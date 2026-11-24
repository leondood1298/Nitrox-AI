using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The copy of another player's torpedo plays the launch sound one frame after the shot was received, when Unity
///     calls its Start: that sound repeats the other player's action (not sent, faded by distance), as if it had been
///     played while the shot was handled.
/// </summary>
public sealed partial class SeamothTorpedo_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SeamothTorpedo t) => t.Start());

    public static void Prefix(SeamothTorpedo __instance, out ReplayedSoundScope? __state)
    {
        __state = __instance.GetComponent<BulletManager.RemotePlayerBullet>() ? ReplayedSoundScope.Enter() : null;
    }

    public static void Finalizer(ReplayedSoundScope? __state)
    {
        __state?.Dispose();
    }
}
