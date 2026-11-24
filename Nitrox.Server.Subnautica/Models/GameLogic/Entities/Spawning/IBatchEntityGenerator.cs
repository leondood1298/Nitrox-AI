using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;

/// <summary>
///     Makes the natural content of one batch of the world. It keeps nothing: which batches are done is
///     <see cref="SpawnedBatches" />'s to know, and the entities belong to the registry once made.
/// </summary>
internal interface IBatchEntityGenerator
{
    Task<List<Entity>> GenerateAsync(NitroxInt3 batchId, bool fullCacheCreation);
}
