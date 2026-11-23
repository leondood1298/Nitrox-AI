using System.Collections.Generic;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     Decides when the action a simulated creature chose has to be sent. The game chooses an action every time the
///     creature's behaviour updates, which is usually the same one as before, so sending each choice would mean a packet
///     per creature per frame.
/// </summary>
public sealed class CreatureActionBroadcastFilter(float resendAfterSeconds, int maxTrackedCreatures = 512)
{
    private readonly Dictionary<NitroxId, LastSent> lastSentByCreature = [];

    /// <summary>
    ///     True when the action differs from the last one sent for this creature, or when that was long enough ago that
    ///     players who loaded the creature in the meantime should be told.
    /// </summary>
    public bool ShouldSend(NitroxId creatureId, string actionTypeName, float now)
    {
        if (lastSentByCreature.TryGetValue(creatureId, out LastSent last) && last.ActionTypeName == actionTypeName && now - last.Time < resendAfterSeconds)
        {
            return false;
        }

        if (lastSentByCreature.Count >= maxTrackedCreatures && !lastSentByCreature.ContainsKey(creatureId))
        {
            // Creatures that died or went out of range are never removed one by one. Starting over costs one extra
            // packet for each creature that is still around.
            lastSentByCreature.Clear();
        }
        lastSentByCreature[creatureId] = new LastSent(actionTypeName, now);
        return true;
    }

    private readonly record struct LastSent(string ActionTypeName, float Time);
}
