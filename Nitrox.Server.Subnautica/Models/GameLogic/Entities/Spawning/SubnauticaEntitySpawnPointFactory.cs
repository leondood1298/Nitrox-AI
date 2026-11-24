using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.UnityStubs;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;

public class SubnauticaEntitySpawnPointFactory : EntitySpawnPointFactory
{
    public override List<EntitySpawnPoint> From(AbsoluteEntityCell absoluteEntityCell, NitroxTransform transform, GameObject gameObject, Dictionary<string, EntitySpawnPoint> spawnPointsById)
    {
        List<EntitySpawnPoint> spawnPoints = [];
        EntitySlotsPlaceholder entitySlotsPlaceholder = gameObject.GetComponent<EntitySlotsPlaceholder>();

        if (gameObject.CreateEmptyObject)
        {
            SerializedEntitySpawnPoint entitySpawnPoint = new(gameObject.SerializedComponents, gameObject.Layer, absoluteEntityCell, transform);

            // Placed once, under its parent or at the top: adding it to the top as well spawned every baked light twice
            HandleParenting(spawnPoints, entitySpawnPoint, gameObject, spawnPointsById);
        }
        else if (!ReferenceEquals(entitySlotsPlaceholder, null))
        {
            foreach (EntitySlotData entitySlotData in entitySlotsPlaceholder.slotsData)
            {
                List<EntitySlot.Type> slotTypes = SlotsHelper.ConvertSlotTypes(entitySlotData.allowedTypes);
                List<string> stringSlotTypes = slotTypes.Select(s => s.ToString()).ToList();
                EntitySpawnPoint entitySpawnPoint = new(absoluteEntityCell,
                                                        entitySlotData.localPosition.ToDto(),
                                                        entitySlotData.localRotation.ToDto(),
                                                        stringSlotTypes,
                                                        entitySlotData.density,
                                                        entitySlotData.biomeType.ToString());

                HandleParenting(spawnPoints, entitySpawnPoint, gameObject, spawnPointsById);
            }
        }
        else
        {
            EntitySpawnPoint entitySpawnPoint = new(absoluteEntityCell, transform.LocalPosition, transform.LocalRotation, transform.LocalScale, gameObject.ClassId);

            HandleParenting(spawnPoints, entitySpawnPoint, gameObject, spawnPointsById);
        }

        return spawnPoints;
    }

    private static void HandleParenting(List<EntitySpawnPoint> spawnPoints, EntitySpawnPoint entitySpawnPoint, GameObject gameObject, Dictionary<string, EntitySpawnPoint> spawnPointsById)
    {
        if (gameObject.Parent != null && spawnPointsById.TryGetValue(gameObject.Parent, out EntitySpawnPoint parent))
        {
            entitySpawnPoint.Parent = parent;
            parent.Children.Add(entitySpawnPoint);
        }

        spawnPointsById[gameObject.Id] = entitySpawnPoint;

        if (gameObject.Parent == null)
        {
            spawnPoints.Add(entitySpawnPoint);
        }
    }
}
