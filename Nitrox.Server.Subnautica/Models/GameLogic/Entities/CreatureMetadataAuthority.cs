using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Keeps a creature's infection and its other lasting state in its one metadata slot. Clients send a creature's
///     infection as <see cref="CreatureInfectionMetadata" /> whatever creature metadata it has, so that a cured Reaper keeps
///     its leash position and a Sea Treader its path.
/// </summary>
internal static class CreatureMetadataAuthority
{
    /// <summary>
    ///     Only the player who simulates a creature decides its infection; the others apply what they are told.
    /// </summary>
    public static bool IsFromSimulatingPlayer(Player? simulatingPlayer, Player sender)
    {
        // Compared by session: Player's == compares the PeerId
        return simulatingPlayer != null && simulatingPlayer.SessionId == sender.SessionId;
    }

    /// <returns>What to store, or null when the update must be refused.</returns>
    public static CreatureMetadata? Merge(EntityMetadata? stored, CreatureMetadata requested)
    {
        if (requested is CreatureInfectionMetadata infection)
        {
            switch (stored)
            {
                case CreatureMetadata storedCreature:
                    // A hero peeper's enzyme is sent with an infection nobody has decided yet
                    if (CreatureMetadata.IsDecided(infection.InfectedAmount))
                    {
                        storedCreature.InfectedAmount = infection.InfectedAmount;
                    }
                    storedCreature.EnzymeEndTime = Math.Max(storedCreature.EnzymeEndTime, infection.EnzymeEndTime);
                    // The leash and size of a creature are sent the same way, by its simulating player
                    if (infection.HasLeash)
                    {
                        storedCreature.WithLeash(infection.LeashPosition);
                    }
                    if (infection.HasSize)
                    {
                        storedCreature.WithSize(infection.Size);
                    }
                    return storedCreature;
                case null:
                    return infection;
                default:
                    // Another kind of state (a corpse's decay) would be lost
                    return null;
            }
        }

        // A baby's drops only add up: a copy that missed some must not give it its dropped balls back
        if (requested is SeaEmperorBabyMetadata baby && stored is SeaEmperorBabyMetadata storedBaby && storedBaby.DropCount > baby.DropCount)
        {
            requested = new SeaEmperorBabyMetadata(baby.Scale, storedBaby.DropCount, baby.DropLimit, baby.InfectedAmount, baby.EnzymeEndTime);
        }

        // Infection only changes through CreatureInfectionMetadata, which keeps the stored value up to date. The value in
        // other creature metadata comes from the sender's copy and only counts when nothing has been decided yet.
        if (stored is CreatureMetadata storedMetadata)
        {
            if (CreatureMetadata.IsDecided(storedMetadata.InfectedAmount))
            {
                requested.InfectedAmount = storedMetadata.InfectedAmount;
            }
            requested.EnzymeEndTime = Math.Max(requested.EnzymeEndTime, storedMetadata.EnzymeEndTime);
            // Metadata of the creature's own kind (a Stalker's friend, a Sand Shark's burrow) does not carry these
            requested.KeepKnownBodyFrom(storedMetadata);
        }
        return requested;
    }
}
