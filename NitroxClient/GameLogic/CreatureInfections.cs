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
///     - the roll for a creature that nobody has decided yet is fixed per creature id, so a healthy creature needs nothing stored
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

    private bool applyingReceivedValue;

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
        CreatureInfectionMetadata metadata = new(amount);
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
        if (!creature.TryGetNitroxId(out NitroxId creatureId) || decidedIds.Contains(creatureId) || !simulationOwnership.HasAnyLockType(creatureId))
        {
            return;
        }
        // A corpse keeps the infection it had (its metadata is its decay)
        LiveMixin liveMixin = creature.GetComponent<LiveMixin>();
        if (liveMixin && !liveMixin.IsAlive())
        {
            return;
        }
        // Gives the same answer every time for the same creature, so rolling twice changes nothing
        creature.ProcessInfection();
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
