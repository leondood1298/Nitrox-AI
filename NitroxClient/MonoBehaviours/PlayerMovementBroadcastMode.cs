namespace NitroxClient.MonoBehaviours;

/// <summary>
///     What <see cref="PlayerMovementBroadcaster" /> sends of the local player in a frame.
/// </summary>
internal enum PlayerMovementBroadcastMode
{
    /// <summary>
    ///     Nothing: the game's camera is not the player's, as with the free camera.
    /// </summary>
    SUPPRESSED,

    NORMAL,

    /// <summary>
    ///     The player flies a Scanner Room camera. The game's camera is at the drone; the player's body stands at the
    ///     room's screen, and that is where the other players have to see it.
    /// </summary>
    SCANNER_CAMERA_ANCHOR
}
