using System.Globalization;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic;

/// <summary>
///     Plant diagnostics ([PLD1], the client's half; the server logs what it kept). In the first live test the server's
///     fruit of a lantern tree followed neither what the player saw nor the fruit the player picked, and the cause could not
///     be told from the logs. Each line says which fruit this game's plant has, whether that plant is the one its seed
///     knows as grown (the one the server's state is applied to), and how many grown plants of that seed the planter holds:
///     more than one, or a plant that is not the seed's, would explain a picked fruit that never reached the server.
/// </summary>
public static class PlantFruitDiagnostics
{
    /// <param name="ev">What happened: pick, grow or apply.</param>
    /// <param name="index">The fruit picked, or -1.</param>
    public static void Report(string ev, FruitPlant fruitPlant, Plantable plantable, int index)
    {
        if (!fruitPlant || !plantable)
        {
            return;
        }
        bool[] pickedStates = fruitPlant.fruits.Select(fruit => fruit && fruit.pickedState).ToArray();
        string present = string.Join(",", FruitPlantMetadata.PresentIndices(pickedStates));
        // A fruit shown although picked, or hidden although on the plant (the game keeps both together in SetPickedState)
        string shownApart = string.Join(",", Enumerable.Range(0, fruitPlant.fruits.Length)
                                                       .Where(i => fruitPlant.fruits[i] && fruitPlant.fruits[i].gameObject.activeSelf == fruitPlant.fruits[i].pickedState));
        bool linked = plantable.linkedGrownPlant && plantable.linkedGrownPlant.gameObject == fruitPlant.gameObject;
        int grownOfSeed = -1;
        if (plantable.currentPlanter && plantable.currentPlanter.grownPlantsRoot)
        {
            grownOfSeed = plantable.currentPlanter.grownPlantsRoot.GetComponentsInChildren<GrownPlant>(true).Count(grown => grown.seed == plantable);
        }
        plantable.TryGetNitroxId(out NitroxId plantId);
        float time = DayNightCycle.main ? DayNightCycle.main.timePassedAsFloat : -1f;
        string where = $"linked={linked} grownOfSeed={grownOfSeed} inactive={fruitPlant.inactiveFruits.Count} shownApart=[{shownApart}] object={fruitPlant.GetInstanceID()} active={fruitPlant.gameObject.activeInHierarchy}";
        Log.Info($"[PLD1] side=C ev={ev} plant={plantId} index={index} present=[{present}]/{pickedStates.Length} next={fruitPlant.timeNextFruit.ToString(CultureInfo.InvariantCulture)} time={time.ToString(CultureInfo.InvariantCulture)} {where}");
    }
}
