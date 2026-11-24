using System.Linq;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Picked fruit, seeds and samples are synced and kept as <see cref="FruitPlant" /> state (FruitPlantMetadata). Most
///     plants whose parts are picked have no FruitPlant in the game: a Creepvine, a blood root or blood kelp (Blood Oil),
///     a small fan (seeds), a grown Chinese potato plant. The game keeps their picked parts with its own save, which the
///     mod does not use. They get a FruitPlant that never grows anything back (as the game's FruitPlant does until
///     <c>FruitPlant.OnGrown</c>, which only a plant grown in a planter gets), so that what was picked stays picked for
///     everybody, for a player who comes later and after a restart.
/// </summary>
public static class PickablePlants
{
    /// <summary>
    ///     Gives <paramref name="plant" /> a FruitPlant over the parts that are picked off it, when it has none.
    /// </summary>
    public static void Track(GameObject plant)
    {
        // An item (a Blood Oil lying around holds the model of its plant) is no plant
        if (plant.GetComponent<FruitPlant>() || plant.GetComponent<Pickupable>())
        {
            return;
        }
        PickPrefab[] parts = plant.GetComponentsInChildren<PickPrefab>(true).Where(part => IsTrackedPart(part.destroyOnPicked)).ToArray();
        if (parts.Length == 0)
        {
            return;
        }

        FruitPlant fruitPlant = plant.AddComponent<FruitPlant>();
        fruitPlant.fruitSpawnEnabled = false;
        fruitPlant.timeNextFruit = -1;
        fruitPlant.fruits = parts;
    }

    /// <summary>
    ///     A part that destroys itself when picked (a melon, a whole Chinese potato plant, a Purple Vase plant's seeds) is
    ///     removed for everybody with its object instead (PickPrefab_SetPickedState_Patch).
    /// </summary>
    public static bool IsTrackedPart(bool destroyOnPicked) => !destroyOnPicked;
}
