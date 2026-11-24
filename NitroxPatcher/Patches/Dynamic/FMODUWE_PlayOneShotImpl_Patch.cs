using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using Nitrox.Model.GameLogic.FMOD;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FMODUWE_PlayOneShotImpl_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => FMODUWE.PlayOneShotImpl(default(string), default(Vector3), default(float)));

    public static bool Prefix(string eventPath, Vector3 position, ref float volume, out float __state)
    {
        __state = volume;
        if (FMODSoundSuppressor.SuppressFMODEvents)
        {
            return false;
        }
        if (ReplayedSoundScope.IsActive && FMODSystem.TryGetReplayedVolume(Resolve<FMODWhitelist>(), eventPath, position, volume, out float replayedVolume))
        {
            volume = replayedVolume;
        }
        return true;
    }

    public static void Postfix(string eventPath, Vector3 position, float __state, bool __runOriginal)
    {
        if (__runOriginal && Resolve<FMODWhitelist>().IsWhitelisted(eventPath))
        {
            Resolve<FMODSystem>().SendAssetPlay(eventPath, position.ToDto(), __state);
        }
    }
}
