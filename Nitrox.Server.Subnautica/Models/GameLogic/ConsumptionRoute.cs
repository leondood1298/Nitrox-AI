namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What the server does with energy a player took from a base power source that player does not simulate, see
///     <see cref="BasePowerManager.RouteConsumption" />.
/// </summary>
internal enum ConsumptionRoute
{
    REJECTED,

    /// <summary>
    ///     Passed on to the source's simulating player, whose level is the one that counts.
    /// </summary>
    FORWARD_TO_SIMULATOR,

    /// <summary>
    ///     Nobody simulates the source: taken from the stored level by the server itself.
    /// </summary>
    APPLIED_BY_SERVER
}
