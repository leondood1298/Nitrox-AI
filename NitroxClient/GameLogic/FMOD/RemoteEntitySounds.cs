using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     Plays and stops the sounds other players' games send for an entity, and keeps their loops for an entity this game
///     loads later.
/// </summary>
public sealed class RemoteEntitySounds(RemoteSoundLoops loops)
{
    private readonly RemoteSoundLoops loops = loops;

    public void Apply(NitroxId id, string assetPath, bool play, bool looping, float volume, bool allowFadeout)
    {
        if (!play)
        {
            loops.Stopped(id, assetPath);
        }
        else if (looping)
        {
            loops.Started(id, assetPath, volume);
        }

        // Not loaded here: a loop starts when the entity loads, anything else has passed by then
        if (!NitroxEntity.TryGetObjectFrom(id, out GameObject entity))
        {
            return;
        }
        if (!entity.TryGetComponent(out FMODEmitterController controller))
        {
            controller = entity.AddComponent<FMODEmitterController>();
            controller.LateRegisterEmitter();
            if (looping && play)
            {
                // Registering already started the loop that was just recorded
                return;
            }
        }

        if (play)
        {
            controller.Play(assetPath, volume);
        }
        else
        {
            controller.Stop(assetPath, allowFadeout);
        }
    }
}
