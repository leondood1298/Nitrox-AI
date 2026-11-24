using System;
using Nitrox.Model.DataStructures.Unity;

namespace NitroxClient.GameLogic;

/// <summary>
///     Decides whether the local player's movement is worth a packet. A moving or turning player is sent every frame as
///     before, so remote players look the same; a player standing still sends one last packet with no speed (on which
///     the others' games put the body exactly in place) and then only a resync every few seconds, instead of a packet
///     every frame.
/// </summary>
/// <remarks>
///     Adapted from upstream pull request 2804, without its 30 Hz cap (remote players would move less smoothly) and
///     without its change to base-relative positions, which this fork's receiver and server do not expect.
///     Unity-free, so that it can be tested.
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

    private bool hasSent;
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

        if (send)
        {
            hasSent = true;
            lastPosition = position;
            lastBodyRotation = bodyRotation;
            lastAimingRotation = aimingRotation;
            lastSentTime = now;
        }
        return send;
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
