using UnityEngine;

namespace NitroxClient.GameLogic.PlayerLogic;

/// <summary>
///     How fast another player walks inside a Cyclops, in the Cyclops' own frame, worked out from the positions they send
///     and this game's clock. The sender's frame rate and send rate do not matter, and a player who stops sending (one
///     standing still sends only now and then) is taken to stand still.
/// </summary>
public sealed class CyclopsWalkVelocity
{
    /// <summary>
    ///     Packets closer together than this arrived in the same frame; the speed is measured over a longer span instead.
    /// </summary>
    public const float MINIMUM_INTERVAL = 0.01f;

    /// <summary>
    ///     Without a packet for this long the player is taken to stand still.
    /// </summary>
    public const float EXPIRY_SECONDS = 0.25f;

    /// <summary>
    ///     A jump further than walking (a teleport, a missed stretch of packets) is not shown as a sprint.
    /// </summary>
    public const float MAXIMUM_SPEED = 10f;

    private bool hasReference;
    private Vector3 referencePosition;
    private float referenceTime;
    private Vector3 velocity;
    private float lastPacketTime;

    public void Reset()
    {
        hasReference = false;
        velocity = Vector3.zero;
    }

    public void OnPacket(Vector3 localPosition, float now)
    {
        lastPacketTime = now;
        if (!hasReference)
        {
            hasReference = true;
            referencePosition = localPosition;
            referenceTime = now;
            velocity = Vector3.zero;
            return;
        }

        float elapsed = now - referenceTime;
        if (elapsed < MINIMUM_INTERVAL)
        {
            return;
        }

        velocity = Vector3.ClampMagnitude((localPosition - referencePosition) / elapsed, MAXIMUM_SPEED);
        referencePosition = localPosition;
        referenceTime = now;
    }

    public Vector3 GetVelocity(float now)
    {
        if (!hasReference || now - lastPacketTime > EXPIRY_SECONDS)
        {
            return Vector3.zero;
        }
        return velocity;
    }
}
