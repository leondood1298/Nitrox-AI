using UnityEngine;

namespace NitroxClient.GameLogic.PlayerLogic;

/// <summary>
///     Where a body that has just been made stands, from the player entity the server keeps. A player who stands still or
///     is in a cinematic sends no movement for a while, and the body would wait at the world's origin until then.
/// </summary>
public static class RemoteBodyPlacement
{
    /// <param name="localPosition">The entity's position, relative to its parent if it has one.</param>
    /// <param name="localRotation">The entity's rotation, relative to its parent if it has one.</param>
    /// <param name="hasParent">Whether the entity is inside a base, a Cyclops or a lifepod.</param>
    /// <param name="parentPosition">The parent's position in the world.</param>
    /// <param name="parentRotation">The parent's rotation in the world.</param>
    /// <param name="parentIsBase">
    ///     Inside a base the stored rotation cannot be trusted: the movement packet carries a rotation relative to the base,
    ///     which the server stores as if it were the world's. The body then keeps its rotation until the next packet.
    /// </param>
    /// <returns>The world position, and the world rotation if it is known.</returns>
    public static (Vector3 Position, Quaternion? Rotation) Place(Vector3 localPosition, Quaternion localRotation, bool hasParent, Vector3 parentPosition, Quaternion parentRotation, bool parentIsBase)
    {
        if (!hasParent)
        {
            return (localPosition, localRotation);
        }

        Vector3 position = parentPosition + parentRotation * localPosition;
        return (position, parentIsBase ? null : parentRotation * localRotation);
    }
}
