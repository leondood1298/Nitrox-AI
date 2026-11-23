using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.Helpers;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Makes a creature's infection (InfectedMixin) the same for every player and keeps it on the server:<br />
///     - only the player who simulates a creature changes its infection (every writer ends in InfectedMixin.SetInfectedAmount:
///     the roll when it appears, the relapse, enzyme balls, hero peepers, containment spread and cure) and sends the new value<br />
///     - the others apply what they receive, and show a creature healthy until they are told otherwise<br />
///     - the roll for a creature that nobody has decided yet is fixed per creature id, so a healthy creature needs nothing stored<br />
///     A hero peeper's enzyme (Peeper.BecomeHero: born a hero, an enzyme cloud, a Precursor vent) is one fact per peeper in the
///     same way: only its simulating player makes it a hero, the born-hero roll is fixed per id, and the time its enzyme runs
///     out is sent with its infection and applied by everybody.
/// </summary>
/// <remarks>
///     The player's own infection is synced per player (PlayerStats). The fish shown in an Aquarium are local copies without an
///     id and stay local, like any creature that is not an entity.
/// </remarks>
public class CreatureInfections
{
    private readonly SimulationOwnership simulationOwnership;
    private readonly Entities entities;
    private readonly ThrottledPacketSender throttledPacketSender;

    /// <summary>
    ///     Creatures whose infection somebody has decided: received from the server or set by us as their simulating player.
    ///     They are never rolled again.
    /// </summary>
    private readonly HashSet<NitroxId> decidedIds = [];

    /// <summary>
    ///     When the enzyme of hero peepers runs out, received from the server or decided by us as their simulating player.
    /// </summary>
    private readonly Dictionary<NitroxId, float> enzymeEndTimes = [];

    private bool applyingReceivedValue;
    private bool applyingReceivedEnzyme;

    public CreatureInfections(SimulationOwnership simulationOwnership, Entities entities, ThrottledPacketSender throttledPacketSender)
    {
        this.simulationOwnership = simulationOwnership;
        this.entities = entities;
        this.throttledPacketSender = throttledPacketSender;
    }

    /// <summary>
    ///     Called before InfectedMixin.SetInfectedAmount.
    /// </summary>
    /// <returns>True if the infection may change here</returns>
    public bool MayWrite(InfectedMixin infectedMixin)
    {
        if (!TryGetSyncedCreatureId(infectedMixin, out NitroxId creatureId))
        {
            return true;
        }
        return CreatureInfectionRules.MayWrite(simulationOwnership.HasAnyLockType(creatureId), applyingReceivedValue);
    }

    /// <summary>
    ///     Called after InfectedMixin.SetInfectedAmount changed the infection.
    /// </summary>
    public void OnChanged(InfectedMixin infectedMixin, float previousAmount)
    {
        if (applyingReceivedValue || !TryGetSyncedCreatureId(infectedMixin, out NitroxId creatureId) || !simulationOwnership.HasAnyLockType(creatureId))
        {
            return;
        }

        decidedIds.Add(creatureId);
        float amount = infectedMixin.GetInfectedAmount();
        CreatureInfectionMetadata metadata = new(amount, GetEnzymeEndTime(creatureId));
        if (CreatureInfectionRules.IsImmediateChange(previousAmount, amount))
        {
            // A pending throttled value would come after this one and undo it
            throttledPacketSender.RemovePendingPackets(creatureId);
            entities.BroadcastMetadataUpdate(creatureId, metadata);
        }
        else
        {
            entities.BroadcastMetadataUpdateThrottled(creatureId, metadata);
        }
    }

    /// <summary>
    ///     Stands in for Random.value in Creature.ProcessInfection, which infects below 0.05.
    /// </summary>
    public float GetInfectionRoll(Creature creature)
    {
        if (!creature.TryGetNitroxId(out NitroxId creatureId))
        {
            // Not an entity: stays local like in the game
            return Random.value;
        }
        if (!simulationOwnership.HasAnyLockType(creatureId) || decidedIds.Contains(creatureId))
        {
            return CreatureInfectionRules.NEVER_INFECTS;
        }
        return CreatureInfectionRules.Roll(creatureId);
    }

    /// <summary>
    ///     Rolls the infection of a creature that we simulate and nobody has decided yet. Creature.InitializeOnce rolls it
    ///     when the creature appears, which can be before we know we simulate it, or long before we take it over.
    /// </summary>
    public void RollIfUndecided(Creature creature)
    {
        if (!creature.TryGetNitroxId(out NitroxId creatureId) || !simulationOwnership.HasAnyLockType(creatureId))
        {
            return;
        }
        // A corpse keeps the infection it had (its metadata is its decay)
        LiveMixin liveMixin = creature.GetComponent<LiveMixin>();
        if (liveMixin && !liveMixin.IsAlive())
        {
            return;
        }
        if (!decidedIds.Contains(creatureId))
        {
            // Gives the same answer every time for the same creature, so rolling twice changes nothing
            creature.ProcessInfection();
        }
        if (creature is Peeper peeper)
        {
            RollBornHero(peeper);
        }
    }

    /// <summary>
    ///     Stands in for Random.value in Peeper.InitializeOnce, which makes a peeper a hero from birth below 0.05.
    /// </summary>
    public float GetBornHeroRoll(Peeper peeper)
    {
        if (!peeper.TryGetNitroxId(out NitroxId peeperId))
        {
            // Not an entity: stays local like in the game
            return Random.value;
        }
        if (!simulationOwnership.HasAnyLockType(peeperId) ||
            !CreatureInfectionRules.MayBornHeroRecharge(GetEnzymeEndTime(peeperId), DayNightCycle.main.timePassedAsFloat, peeper.rechargeInterval))
        {
            return CreatureInfectionRules.NEVER_INFECTS;
        }
        return CreatureInfectionRules.BornHeroRoll(peeperId);
    }

    /// <summary>
    ///     Called before Peeper.BecomeHero.
    /// </summary>
    /// <returns>True if the peeper may become a hero here</returns>
    public bool MayBecomeHero(Peeper peeper)
    {
        if (!peeper.TryGetNitroxId(out NitroxId peeperId))
        {
            return true;
        }
        return applyingReceivedEnzyme || simulationOwnership.HasAnyLockType(peeperId);
    }

    /// <summary>
    ///     Called after Peeper.BecomeHero gave the peeper its enzyme.
    /// </summary>
    public void OnBecameHero(Peeper peeper)
    {
        if (applyingReceivedEnzyme || !peeper.TryGetNitroxId(out NitroxId peeperId) || !simulationOwnership.HasAnyLockType(peeperId))
        {
            return;
        }

        float enzymeEndTime = CreatureInfectionRules.GetEnzymeEndTime(DayNightCycle.main.timePassedAsFloat, peeper.enzymeAmount, peeper._inPrisonAquarium);
        enzymeEndTimes[peeperId] = enzymeEndTime;
        // Carries the infection as it is now (BecomeHero cures it), so a pending throttled value is outdated
        throttledPacketSender.RemovePendingPackets(peeperId);
        entities.BroadcastMetadataUpdate(peeperId, new CreatureInfectionMetadata(GetDecidedAmount(peeper), enzymeEndTime));
    }

    /// <summary>
    ///     Applies the infection and enzyme carried by a creature's metadata.
    /// </summary>
    public void ApplyReceived(GameObject gameObject, CreatureMetadata metadata)
    {
        ApplyReceived(gameObject, metadata.InfectedAmount);
        ApplyReceivedEnzyme(gameObject, metadata.EnzymeEndTime);
    }

    /// <summary>
    ///     The decided infection and enzyme of a creature that has no other creature metadata. A fish that is picked up,
    ///     dropped or moved into a container is sent as a new entity with what is extracted from it, so without this its
    ///     decided state was lost and it fell back to its roll.
    /// </summary>
    /// <returns>Null when there is nothing decided, or when other metadata carries the creature's state.</returns>
    public CreatureInfectionMetadata? GetDecidedState(InfectedMixin infectedMixin)
    {
        if (infectedMixin.player || !infectedMixin.TryGetComponent(out Creature creature) || creature is SeaTreader)
        {
            return null;
        }
        // A contained fish carries its state in its growth (WaterParkCreatureMetadata), a corpse has only its decay
        if (creature.TryGetComponent(out WaterParkCreature waterParkCreature) && waterParkCreature.age != -1)
        {
            return null;
        }
        if (creature.TryGetComponent(out LiveMixin liveMixin) && !liveMixin.IsAlive())
        {
            return null;
        }

        float infectedAmount = GetDecidedAmount(creature);
        float enzymeEndTime = GetDecidedEnzymeEndTime(creature);
        if (!CreatureInfectionRules.HasDecidedState(infectedAmount, enzymeEndTime))
        {
            return null;
        }
        return new CreatureInfectionMetadata(infectedAmount, enzymeEndTime);
    }

    /// <summary>
    ///     What the creature metadata extractors write for a hero peeper's enzyme.
    /// </summary>
    public float GetDecidedEnzymeEndTime(Component creatureComponent)
    {
        return creatureComponent.TryGetNitroxId(out NitroxId creatureId) ? GetEnzymeEndTime(creatureId) : CreatureMetadata.NO_ENZYME;
    }

    private void ApplyReceivedEnzyme(GameObject gameObject, float enzymeEndTime)
    {
        if (enzymeEndTime <= CreatureMetadata.NO_ENZYME || !gameObject.TryGetNitroxId(out NitroxId peeperId))
        {
            return;
        }
        // A later time always wins (see CreatureMetadata.EnzymeEndTime)
        enzymeEndTime = Mathf.Max(enzymeEndTime, GetEnzymeEndTime(peeperId));
        enzymeEndTimes[peeperId] = enzymeEndTime;

        if (!gameObject.TryGetComponent(out Peeper peeper))
        {
            return;
        }
        float enzymeLeft = CreatureInfectionRules.GetEnzymeLeft(enzymeEndTime, DayNightCycle.main.timePassedAsFloat, peeper.enzymeDuration);
        if (enzymeLeft <= 0f)
        {
            return;
        }

        applyingReceivedEnzyme = true;
        try
        {
            // Gives the peeper its glow and healing trigger. Its cure is sent by its simulating player with its infection.
            peeper.BecomeHero();
            peeper.enzymeAmount = enzymeLeft;
        }
        finally
        {
            applyingReceivedEnzyme = false;
        }
    }

    /// <summary>
    ///     Peeper.InitializeOnce makes a peeper a hero from birth when it appears, which can be before we know that we simulate
    ///     it. Does it once that is known.
    /// </summary>
    private void RollBornHero(Peeper peeper)
    {
        LargeWorld largeWorld = LargeWorld.main;
        if (!largeWorld || string.Equals("safeShallows", largeWorld.GetBiome(peeper.transform.position), System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (GetBornHeroRoll(peeper) < CreatureInfectionRules.BORN_HERO_CHANCE)
        {
            // What Peeper.InitializeOnce does
            peeper.timeRechargeEnzyme = 1f;
            peeper.BecomeHero();
        }
    }

    private float GetEnzymeEndTime(NitroxId creatureId)
    {
        return enzymeEndTimes.TryGetValue(creatureId, out float enzymeEndTime) ? enzymeEndTime : CreatureMetadata.NO_ENZYME;
    }

    public void RollIfUndecided(NitroxId entityId)
    {
        if (NitroxEntity.TryGetComponentFrom(entityId, out Creature creature))
        {
            RollIfUndecided(creature);
        }
    }

    /// <summary>
    ///     Applies the infection carried by a creature's metadata. Called by the processors of every creature metadata.
    /// </summary>
    public void ApplyReceived(GameObject gameObject, float infectedAmount)
    {
        if (!CreatureMetadata.IsDecided(infectedAmount))
        {
            return;
        }
        if (gameObject.TryGetNitroxId(out NitroxId creatureId))
        {
            decidedIds.Add(creatureId);
        }
        if (!gameObject.TryGetComponent(out InfectedMixin infectedMixin))
        {
            return;
        }

        applyingReceivedValue = true;
        try
        {
            infectedMixin.SetInfectedAmount(infectedAmount);
        }
        finally
        {
            applyingReceivedValue = false;
        }
    }

    /// <summary>
    ///     What the creature metadata extractors write: the creature's infection if somebody has decided it, otherwise
    ///     <see cref="CreatureMetadata.UNDECIDED" /> so that the server keeps or later gets the decided value.
    /// </summary>
    public float GetDecidedAmount(Component creatureComponent)
    {
        if (creatureComponent.TryGetNitroxId(out NitroxId creatureId) && decidedIds.Contains(creatureId) &&
            creatureComponent.TryGetComponent(out InfectedMixin infectedMixin))
        {
            return infectedMixin.GetInfectedAmount();
        }
        return CreatureMetadata.UNDECIDED;
    }

    /// <summary>
    ///     Players (Player.infectedMixin), remote players' cosmetic mixin and the Aquarium's display copies have no Creature.
    /// </summary>
    private static bool TryGetSyncedCreatureId(InfectedMixin infectedMixin, [NotNullWhen(true)] out NitroxId? creatureId)
    {
        creatureId = null;
        return !infectedMixin.player && infectedMixin.GetComponent<Creature>() && infectedMixin.TryGetNitroxId(out creatureId);
    }
}
