using System;
using UnityEngine;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     Marks an object on which this game will shortly do (through <c>Invoke</c>) what another player already did and
///     already sends the sound of: when it happens, the game plays and sends nothing. The game's own later calls, without
///     the mark, sound as usual.
/// </summary>
public sealed class DelayedSilentReplay : MonoBehaviour
{
    public static void Mark(Component target) => target.gameObject.EnsureComponent<DelayedSilentReplay>();

    public static void Unmark(Component target)
    {
        if (target.TryGetComponent(out DelayedSilentReplay mark))
        {
            DestroyImmediate(mark);
        }
    }

    /// <returns>The silence to end once the call is over, or null when the object was not marked.</returns>
    public static Silence TakeMark(Component target)
    {
        if (!target.TryGetComponent(out DelayedSilentReplay mark))
        {
            return null;
        }
        DestroyImmediate(mark);
        return new Silence();
    }

    public sealed class Silence : IDisposable
    {
        private readonly FMODSoundSuppressor soundSuppressor = FMODSoundSuppressor.Suppress();
        private readonly ReplayedSoundScope replayedSoundScope = ReplayedSoundScope.Enter();

        public void Dispose()
        {
            replayedSoundScope.Dispose();
            soundSuppressor.Dispose();
        }
    }
}
