using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using UnityEngine;
using static NitroxClient.GameLogic.Spawning.Metadata.Extractor.CyclopsMetadataExtractor;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class CyclopsMetadataExtractor : EntityMetadataExtractor<CyclopsGameObject, CyclopsMetadata>
{
    public override CyclopsMetadata Extract(CyclopsGameObject cyclops)
    {
        GameObject gameObject = cyclops.GameObject;
        CyclopsSilentRunningAbilityButton silentRunning = gameObject.RequireComponentInChildren<CyclopsSilentRunningAbilityButton>(true);

        CyclopsEngineChangeState engineState = gameObject.RequireComponentInChildren<CyclopsEngineChangeState>(true);
        bool engineShuttingDown = (engineState.motorMode.engineOn && engineState.invalidButton);
        bool engineOn = (engineState.startEngine || engineState.motorMode.engineOn) && !engineShuttingDown;

        CyclopsShieldButton shield = gameObject.GetComponentInChildren<CyclopsShieldButton>(true);
        bool shieldOn = (shield) ? shield.active : false;

        CyclopsSonarButton sonarButton = gameObject.GetComponentInChildren<CyclopsSonarButton>(true);
        bool sonarOn = (sonarButton) ? sonarButton._sonarActive : false;

        CyclopsMotorMode.CyclopsMotorModes motorMode = engineState.motorMode.cyclopsMotorMode;

        LiveMixin liveMixin = gameObject.RequireComponentInChildren<LiveMixin>();
        float health = liveMixin.health;

        SubRoot subRoot = gameObject.RequireComponentInChildren<SubRoot>();
        bool isDestroyed = subRoot.subDestroyed || health <= 0f;

        // Leaks and fires only exist once the Cyclops' Start methods have run; before that they are reported as unknown.
        List<int> damagePointIndexes = subRoot.damageManager && subRoot.damageManager.damageRecievers != null ? [..CyclopsDamageSync.GetActiveDamagePoints(subRoot)] : null;
        List<CyclopsFireData> fires = gameObject.TryGetComponent(out SubFire subFire) && subFire.roomFires.Count > 0 ? [..CyclopsDamageSync.GetActiveRoomFires(subFire)] : null;

        return new(silentRunning.active, shieldOn, sonarOn, engineOn, (int)motorMode, health, isDestroyed, damagePointIndexes, fires);
    }

    public struct CyclopsGameObject
    {
        public GameObject GameObject { get; set; }
    }
}
