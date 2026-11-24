using System;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     Silences the sounds the game starts while it is held, but never a stop: a sound that should end must end.
///     Scopes nest; the game is silent until the outermost one ends.
/// </summary>
public readonly struct FMODSoundSuppressor : IDisposable
{
    private static int suppressCount;

    public static bool SuppressFMODEvents => suppressCount > 0;

    public static FMODSoundSuppressor Suppress()
    {
        suppressCount++;
        return default;
    }

    public void Dispose()
    {
        if (suppressCount > 0)
        {
            suppressCount--;
        }
    }
}
