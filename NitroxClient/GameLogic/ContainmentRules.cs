namespace NitroxClient.GameLogic;

/// <summary>
///     Who decides what happens to the life in an Alien Containment: the player who simulates the containment. Every game
///     runs the containment's timers on the shared clock; only that player's game hatches an egg, makes a newborn or
///     replaces a grown creature, and tells the others.
/// </summary>
public static class ContainmentRules
{
    /// <param name="containmentHasId">False for a containment that is not synced (nothing to wait for).</param>
    /// <param name="simulatesContainment">True when the local player simulates the containment.</param>
    public static bool DecidesHere(bool containmentHasId, bool simulatesContainment)
    {
        return !containmentHasId || simulatesContainment;
    }
}
