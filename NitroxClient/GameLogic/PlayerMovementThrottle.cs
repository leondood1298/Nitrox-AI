using System;
using Nitrox.Model.DataStructures.Unity;

namespace NitroxClient.GameLogic;

/// <summary>
///     Decides whether the local player's movement is worth a packet. A moving or turning player is sent every frame, up
///     to <see cref="MAX_SENDS_PER_SECOND" />; a player standing still sends one last packet with no speed (on which the
///     others' games put the body exactly in place) and then only a resync every few seconds, instead of a packet every
///     frame. Used for walking in the world and for walking inside a Cyclops alike.
/// </summary>
/// <remarks>
///     Adapted from upstream pull request 2804, with a 60 Hz cap instead of its 30 Hz (remote players move as smoothly
///     as on a 60 Hz screen) and without its change to base-relative positions, which this fork's receiver and server do
///     not expect. Unity-free, so that it can be tested.
/// </remarks>
public sealed class PlayerMovementThrottle
{
    /// <summary>Movement below this (in metres) since the last packet is not worth sending.</summary>
    public const float MINIMAL_MOVEMENT = 0.01f;

    /// <summary>Turning below this (in degrees) since the last packet is not worth sending.</summary>
    public const float MINIMAL_ROTATION = 0.5f;

    /// <summary>Below this speed (in metres per second) the player stands still.</summary>
    public const float STANDING_STILL_SPEED = 0.05f;

    /// <summary>A still player is sent again this often (in seconds), in case a packet was lost.</summary>
    public const float RESYNC_PERIOD = 2f;

    /// <summary>The most packets sent in a second, whatever the frame rate.</summary>
    public const int MAX_SENDS_PER_SECOND = 60;

    /// <summary>
    ///     How early (in seconds) a frame may come and still send: frame times wobble, and at 60 frames a second a strict
    ///     limit would skip every frame that came a little early.
    /// </summary>
    private const float SEND_TIME_TOLERANCE = 0.002f;

    private bool hasSent;
    private float nextSendTime;
    private bool sentStop;
    private NitroxVector3 lastPosition;
    private NitroxQuaternion lastBodyRotation;
    private NitroxQuaternion lastAimingRotation;
    private float lastSentTime;

    public bool ShouldSend(NitroxVector3 position, NitroxQuaternion bodyRotation, NitroxQuaternion aimingRotation, float speed, float now)
    {
        bool moved = !hasSent ||
                     NitroxVector3.Distance(lastPosition, position) > MINIMAL_MOVEMENT ||
                     AngleBetween(lastBodyRotation, bodyRotation) > MINIMAL_ROTATION ||
                     AngleBetween(lastAimingRotation, aimingRotation) > MINIMAL_ROTATION;
        bool standingStill = speed < STANDING_STILL_SPEED;

        bool send;
        if (moved || !standingStill)
        {
            send = true;
            sentStop = false;
        }
        else if (!sentStop)
        {
            // The last packet tells the others the player stopped, with no speed
            send = true;
            sentStop = true;
        }
        else
        {
            send = now - lastSentTime >= RESYNC_PERIOD;
        }

        // At a high frame rate a moving player was a packet per frame, for every other player: at most MAX_SENDS_PER_SECOND
        if (send && hasSent && now < nextSendTime - SEND_TIME_TOLERANCE)
        {
            return false;
        }

        if (send)
        {
            hasSent = true;
            nextSendTime = Math.Max(nextSendTime + 1f / MAX_SENDS_PER_SECOND, now);
            lastPosition = position;
            lastBodyRotation = bodyRotation;
            lastAimingRotation = aimingRotation;
            lastSentTime = now;
        }
        return send;
    }

    /// <summary>
    ///     Forgets what was sent, so that the next call sends: for when the movement is sent another way for a while
    ///     (walking in a Cyclops, then outside it), and the others last heard of it in that other way.
    /// </summary>
    public void Reset()
    {
        hasSent = false;
        sentStop = false;
    }

    /// <summary>
    ///     A player who stands still gets no further packet; the receiver places the body exactly instead of leaving it
    ///     a speed that it would keep forever.
    /// </summary>
    public static bool IsStop(float speed) => speed < STANDING_STILL_SPEED;

    internal static float AngleBetween(NitroxQuaternion a, NitroxQuaternion b)
    {
        float dot = Math.Abs(a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W);
        return (float)(2d * Math.Acos(Math.Min(1f, dot)) * 180d / Math.PI);
    }
}
