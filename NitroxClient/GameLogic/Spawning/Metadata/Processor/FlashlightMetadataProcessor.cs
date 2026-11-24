using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class FlashlightMetadataProcessor : EntityMetadataProcessor<FlashlightMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, FlashlightMetadata metadata)
    {
        FlashLight flashLight = gameObject.GetComponent<FlashLight>();

        if (flashLight)
        {
            ToggleLights lights = flashLight.gameObject.GetComponent<ToggleLights>();

            if (lights)
            {
                lights.lightsActive = metadata.On;
                // As the game's ToggleLights.OnProtoDeserialize does: the flag alone left the light as the prefab had it
                if (lights.lightsParent)
                {
                    lights.lightsParent.SetActive(metadata.On);
                }
            }
            else
            {
                Log.Error($"Could not find ToggleLights on {flashLight.name}");
            }
        }
        else
        {
            Log.Error($"Could not find FlashLight on {gameObject.name}");
        }
    }
}
