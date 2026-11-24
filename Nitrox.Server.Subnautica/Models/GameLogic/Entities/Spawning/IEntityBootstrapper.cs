using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Helper;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;

internal interface IEntityBootstrapper
{
    /// <param name="generator">Gives the ids of the batch being filled.</param>
    /// <param name="random">The random of the batch being filled (see <see cref="Factories.RandomFactory.GetBatchRandom" />).</param>
    public Task<WorldEntity> Prepare(WorldEntity spawnedEntity, DeterministicGenerator generator, XorRandom random);
}
