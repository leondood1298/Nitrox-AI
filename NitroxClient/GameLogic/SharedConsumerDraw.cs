namespace NitroxClient.GameLogic;

/// <summary>
///     Who draws base power for a consumer whose update runs in every player's game, see
///     <see cref="BasePowerConsumptionForwarder.GetSharedConsumerDraw" />.
/// </summary>
public enum SharedConsumerDraw
{
    /// <summary>
    ///     Another player simulates the consumer and draws for it.
    /// </summary>
    NONE,

    /// <summary>
    ///     This client simulates the consumer: it draws, and what comes from sources of other players is reported.
    /// </summary>
    DRAW_AND_REPORT,

    /// <summary>
    ///     The consumer is not a synchronized entity; it behaves as in the game.
    /// </summary>
    LOCAL_ONLY
}
