namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     When another player's body makes a sound that its own game plays only for itself (splashing through the surface,
///     the Seaglide's engine). Unity-free so that it can be tested.
/// </summary>
public static class RemotePlayerSoundRules
{
    /// <summary>The ocean surface is at height 0 everywhere in the game.</summary>
    public const float SURFACE_LEVEL = 0f;

    /// <summary>
    ///     Slower than this, a held Seaglide idles: swimming without it stays below, the Seaglide pulls well above. The
    ///     other player's input is not sent, so its speed is the best sign of the engine running.
    /// </summary>
    public const float SEAGLIDE_RUNNING_SPEED = 5f;

    /// <summary>A splash at most this often, so that bobbing at the surface does not make a splash every frame.</summary>
    public const float SPLASH_INTERVAL = 1f;

    public static bool CrossedSurface(float previousY, float currentY) =>
        (previousY < SURFACE_LEVEL && currentY >= SURFACE_LEVEL) || (previousY >= SURFACE_LEVEL && currentY < SURFACE_LEVEL);

    public static bool ShouldSplash(float previousY, float currentY, bool insideSubOrVehicle, float timeSinceLastSplash) =>
        !insideSubOrVehicle && timeSinceLastSplash >= SPLASH_INTERVAL && CrossedSurface(previousY, currentY);

    public static bool SeaglideRuns(float speed, bool underwater) => underwater && speed >= SEAGLIDE_RUNNING_SPEED;
}
