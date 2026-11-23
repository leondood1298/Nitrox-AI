using System;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class SeaTreaderMetadataProcessor : EntityMetadataProcessor<SeaTreaderMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, SeaTreaderMetadata metadata)
    {
        if (gameObject.TryGetComponent(out SeaTreader seaTreader))
        {
            // Before InitializeOnce, whose infection roll must see that the infection is decided
            Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata.InfectedAmount);

            if (!seaTreader.isInitialized)
            {
                seaTreader.InitializeOnce();
            }

            seaTreader.reverseDirection = metadata.ReverseDirection;

            float grazingTimeLeft = Math.Max(0, metadata.GrazingEndTime - DayNightCycle.main.timePassedAsFloat);

            seaTreader.grazing = grazingTimeLeft > 0;
            seaTreader.grazingTimeLeft = grazingTimeLeft;

            seaTreader.leashPosition = metadata.LeashPosition.ToUnity();
            seaTreader.leashPosition.y = gameObject.transform.position.y;
            seaTreader.isInitialized = true;
            seaTreader.InitializeAgain();
        }
        else
        {
            Log.Error($"Could not find {nameof(SeaTreader)} on {gameObject.name}");
        }
    }
}
