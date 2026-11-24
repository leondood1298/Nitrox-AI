using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     A base spotlight follows the nearest creature it finds (BaseSpotLight.UpdateTarget, every two seconds). Each game
///     found its own, so every player saw the lights aim elsewhere. Now the game that simulates the spotlight (the one
///     drawing its power, see <see cref="BasePowerConsumptionForwarder" />) chooses and tells the others; they follow.
/// </summary>
public class BaseSpotLights
{
    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;

    public BaseSpotLights(IPacketSender packetSender, SimulationOwnership simulationOwnership)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
    }

    internal static SpotLightTargeting Decide(bool hasId, bool simulatedLocally)
    {
        if (!hasId)
        {
            return SpotLightTargeting.CHOOSE_LOCALLY;
        }
        return simulatedLocally ? SpotLightTargeting.CHOOSE_AND_SHARE : SpotLightTargeting.FOLLOW;
    }

    public SpotLightTargeting GetTargeting(BaseSpotLight spotLight)
    {
        bool hasId = spotLight.TryGetNitroxId(out NitroxId id);
        return Decide(hasId, hasId && simulationOwnership.HasAnyLockType(id));
    }

    /// <summary>
    ///     Called after the simulating game's spotlight looked for a target.
    /// </summary>
    public void TargetChosen(BaseSpotLight spotLight, Creature before)
    {
        if (spotLight.trackTarget == before || !spotLight.TryGetNitroxId(out NitroxId spotLightId))
        {
            return;
        }
        // A creature without an id cannot be found in the other games: they sweep instead
        NitroxId? targetId = spotLight.trackTarget && spotLight.trackTarget.TryGetNitroxId(out NitroxId creatureId) ? creatureId : null;
        packetSender.Send(new SpotLightTargetChanged(spotLightId, targetId));
    }

    public static void ApplyTarget(BaseSpotLight spotLight, NitroxId? targetId)
    {
        Creature target = null;
        if (targetId != null && NitroxEntity.TryGetComponentFrom(targetId, out Creature creature))
        {
            target = creature;
        }
        spotLight.trackTarget = target;
    }
}

public enum SpotLightTargeting
{
    /// <summary>Nobody else can know this spotlight: it acts as in the game.</summary>
    CHOOSE_LOCALLY,

    /// <summary>This game simulates the spotlight: it chooses, and tells the others.</summary>
    CHOOSE_AND_SHARE,

    /// <summary>Another game chooses: this one follows what it is told.</summary>
    FOLLOW
}
