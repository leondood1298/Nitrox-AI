using System;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Helpers;

namespace NitroxClient.GameLogic;

/// <summary>
///     A creature's infection, enzyme, leash and size all travel as <see cref="CreatureInfectionMetadata" />, each update
///     leaving the others undecided, and share the creature's slot in <see cref="ThrottledPacketSender" />. A pending update
///     replaced by the next one would lose its change (a leash that moved, then an infection that rose), so the two are
///     merged field by field instead: what the newer one decided wins, the rest is kept from the older one.
/// </summary>
public static class CreatureMetadataUpdates
{
    /// <summary>
    ///     What to send in place of a pending update and the next one for the same entity.
    /// </summary>
    public static EntityMetadataUpdate Merge(EntityMetadataUpdate pending, EntityMetadataUpdate next)
    {
        if (pending.Id != next.Id || pending.NewValue is not CreatureInfectionMetadata older || next.NewValue is not CreatureInfectionMetadata newer)
        {
            return next;
        }
        return new EntityMetadataUpdate(next.Id, Merge(older, newer));
    }

    public static CreatureInfectionMetadata Merge(CreatureInfectionMetadata older, CreatureInfectionMetadata newer)
    {
        float infectedAmount = CreatureMetadata.IsDecided(newer.InfectedAmount) ? newer.InfectedAmount : older.InfectedAmount;
        CreatureInfectionMetadata merged = new(infectedAmount, Math.Max(older.EnzymeEndTime, newer.EnzymeEndTime));
        if (newer.HasLeash || older.HasLeash)
        {
            merged.WithLeash(newer.HasLeash ? newer.LeashPosition : older.LeashPosition);
        }
        if (newer.HasSize || older.HasSize)
        {
            merged.WithSize(newer.HasSize ? newer.Size : older.Size);
        }
        return merged;
    }
}
