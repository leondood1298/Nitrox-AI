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
                    storedCreature.InfectedAmount = infection.InfectedAmount;
                    return storedCreature;
                case null:
                    return infection;
                default:
                    // Another kind of state (a corpse's decay) would be lost
                    return null;
            }
        }

        // Infection only changes through CreatureInfectionMetadata, which keeps the stored value up to date. The value in
        // other creature metadata comes from the sender's copy and only counts when nothing has been decided yet.
        if (stored is CreatureMetadata storedMetadata && CreatureMetadata.IsDecided(storedMetadata.InfectedAmount))
        {
            requested.InfectedAmount = storedMetadata.InfectedAmount;
        }
        return requested;
    }
}
