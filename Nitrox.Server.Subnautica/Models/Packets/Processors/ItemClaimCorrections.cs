using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     What a player is sent whose game took an item another player got first (see <see cref="GameLogic.ItemClaims" />).
/// </summary>
internal static class ItemClaimCorrections
{
    /// <summary>
    ///     The item is where the server has it: the sender's game puts it back there, out of its player's hands.
    /// </summary>
    public static async Task PutBackAsync(AuthProcessorContext context, Entity current, string holderName)
    {
        await context.ReplyAsync(new SpawnEntities(current, forceRespawn: true));
        await context.ReplyAsync(new ItemClaimLost(current.Id, current.TechType, holderName, false, false));
    }

    /// <summary>
    ///     The item only exists in the sender's game, which made it from a source someone else got first: it is removed
    ///     there.
    /// </summary>
    public static async Task RemoveMadeItemAsync(AuthProcessorContext context, Entity made, string winnerName, bool reserved)
    {
        await context.ReplyAsync(new ItemClaimLost(made.Id, made.TechType, winnerName, true, reserved));
    }
}
