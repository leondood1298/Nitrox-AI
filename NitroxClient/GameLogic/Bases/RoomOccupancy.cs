using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using UnityEngine;

namespace NitroxClient.GameLogic.Bases;

/// <summary>
///     Decides whether a piece of a base may be deconstructed in multiplayer: not while the base was just changed by
///     another player or is out of sync (see <see cref="BuildUtils.DeconstructionAllowed" />), and a room or corridor not
///     while any player, this one or another, is inside it.
/// </summary>
public class RoomOccupancy(PlayerManager playerManager)
{
    private readonly PlayerManager playerManager = playerManager;

    public void DeconstructionAllowed(BaseDeconstructable deconstructable, ref bool result, ref string reason)
    {
        if (!result || !BuildingHandler.Main || !deconstructable.deconstructedBase || !deconstructable.deconstructedBase.TryGetNitroxId(out NitroxId baseId))
        {
            return;
        }
        BuildUtils.DeconstructionAllowed(baseId, ref result, ref reason);
        // A face (window, hatch, reinforcement, module) leaves the room standing
        if (!result || deconstructable.face.HasValue)
        {
            return;
        }
        foreach (Vector3 position in PlayerPositions())
        {
            Int3 cell = deconstructable.deconstructedBase.WorldToGrid(position);
            Int3.Bounds bounds = deconstructable.bounds;
            if (IsWithin(cell.x, cell.y, cell.z, bounds.mins.x, bounds.mins.y, bounds.mins.z, bounds.maxs.x, bounds.maxs.y, bounds.maxs.z))
            {
                result = false;
                reason = Language.main.Get("PlayerObstacle");
                return;
            }
        }
    }

    private IEnumerable<Vector3> PlayerPositions()
    {
        if (Player.main)
        {
            yield return Player.main.transform.position;
        }
        foreach (RemotePlayer remotePlayer in playerManager.GetAll())
        {
            if (remotePlayer.Body)
            {
                yield return remotePlayer.Body.transform.position;
            }
        }
    }

    /// <summary>
    ///     Whether a cell is in the cells a piece takes up, bounds included.
    /// </summary>
    internal static bool IsWithin(int x, int y, int z, int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
        x >= minX && x <= maxX && y >= minY && y <= maxY && z >= minZ && z <= maxZ;
}
