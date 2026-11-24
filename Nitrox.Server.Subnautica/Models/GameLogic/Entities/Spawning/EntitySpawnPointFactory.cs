using System.Collections.Generic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.UnityStubs;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;

public abstract class EntitySpawnPointFactory
{
    /// <param name="spawnPointsById">
    ///     The spawn points made so far from the same batch file, by game object id, so a child finds its parent. It belongs
    ///     to one parse: kept longer, it would hold every spawn point of every batch for the whole session.
    /// </param>
    public abstract List<EntitySpawnPoint> From(AbsoluteEntityCell absoluteEntityCell, NitroxTransform transform, GameObject gameObject, Dictionary<string, EntitySpawnPoint> spawnPointsById);
}
