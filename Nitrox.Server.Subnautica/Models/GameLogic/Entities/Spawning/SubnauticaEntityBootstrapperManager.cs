using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Factories;
using Nitrox.Server.Subnautica.Models.Helper;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;

internal sealed class SubnauticaEntityBootstrapperManager(RandomFactory randomFactory, SubnauticaUweWorldEntityFactory subnauticaUweWorldEntityFactory) : IEntityBootstrapperManager
{
    private readonly Dictionary<string, IEntityBootstrapper> entityBootstrappersByClassId = new()
    {
        ["ce0b4131-86e2-444b-a507-45f7b824a286"] = new GeyserBootstrapper(randomFactory.GetUnityLikeRandom(1)), // Geyser.prefab
        ["63462cb4-d177-4551-822f-1904f809ec1f"] = new GeyserBootstrapper(randomFactory.GetUnityLikeRandom(2)), // GeyserShort.prefab
        ["8d3d3c8b-9290-444a-9fea-8e5493ecd6fe"] = new ReefbackBootstrapper(randomFactory.GetUnityLikeRandom(3), subnauticaUweWorldEntityFactory)
    };

    private readonly Dictionary<NitroxTechType, IEntityBootstrapper> entityBootstrappersByTechType = new()
    {
        [TechType.CrashHome.ToDto()] = new CrashHomeBootstrapper(),
        [TechType.ReaperLeviathan.ToDto()] = new StayAtLeashPositionBootstrapper(),
        [TechType.SeaDragon.ToDto()] = new StayAtLeashPositionBootstrapper(),
        [TechType.GhostLeviathan.ToDto()] = new StayAtLeashPositionBootstrapper(),
    };

    /// <summary>
    ///     Creatures whose home is where they spawn: Creature.InitializeOnce takes the spawn position as their leash, which the
    ///     game then saves. Not the Sea Treader, whose leash is a point of its path, nor the Reefback, bootstrapped by class.
    /// </summary>
    private static readonly HashSet<NitroxTechType> creaturesLeashedWhereTheySpawn =
    [
        .. new[]
        {
            TechType.Bladderfish, TechType.Bleeder, TechType.Biter, TechType.Blighter, TechType.BoneShark, TechType.Boomerang, TechType.CaveCrawler,
            TechType.Crabsnake, TechType.CrabSquid, TechType.Crash, TechType.Cutefish, TechType.Eyeye, TechType.GarryFish, TechType.Gasopod,
            TechType.GhostLeviathanJuvenile, TechType.GhostRayBlue, TechType.GhostRayRed, TechType.HoleFish, TechType.Hoopfish, TechType.Hoverfish,
            TechType.Jellyray, TechType.Jumper, TechType.LavaBoomerang, TechType.LavaEyeye, TechType.LavaLarva, TechType.LavaLizard, TechType.Mesmer,
            TechType.Oculus, TechType.Peeper, TechType.RabbitRay, TechType.Reginald, TechType.Rockgrub, TechType.Sandshark, TechType.Shocker,
            TechType.Shuttlebug, TechType.Skyray, TechType.SkyrayNonRoosting, TechType.Spadefish, TechType.SpineEel, TechType.Spinefish, TechType.Stalker,
            TechType.Warper
        }.Select(techType => techType.ToDto())
    ];

    private static readonly IEntityBootstrapper leashBootstrapper = new StayAtLeashPositionBootstrapper();

    public Task<WorldEntity> PrepareEntityIfRequired(WorldEntity spawnedEntity, DeterministicGenerator generator)
    {
        if (entityBootstrappersByTechType.TryGetValue(spawnedEntity.TechType, out IEntityBootstrapper bootstrapper) ||
            (!string.IsNullOrEmpty(spawnedEntity.ClassId) && entityBootstrappersByClassId.TryGetValue(spawnedEntity.ClassId, out bootstrapper)))
        {
            return bootstrapper.Prepare(spawnedEntity, generator);
        }
        if (spawnedEntity.Metadata == null && creaturesLeashedWhereTheySpawn.Contains(spawnedEntity.TechType))
        {
            return leashBootstrapper.Prepare(spawnedEntity, generator);
        }
        return Task.FromResult(spawnedEntity);
    }
}
