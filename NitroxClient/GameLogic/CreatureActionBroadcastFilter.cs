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
    /// <summary>
    ///     Stands for "none of the actions the others replay". It is sent when a creature stops doing one of them, and
    ///     never on its own: most creatures do nothing else, and the others assume it of a creature they hear nothing about.
    /// </summary>
    public const string NO_ACTION = "";

    public bool ShouldSend(NitroxId creatureId, string actionTypeName, float now)
    {
        bool known = lastSentByCreature.TryGetValue(creatureId, out LastSent last);
        if (actionTypeName == NO_ACTION && (!known || last.ActionTypeName == NO_ACTION))
        {
            return false;
        }
        if (known && last.ActionTypeName == actionTypeName && now - last.Time < resendAfterSeconds)
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
