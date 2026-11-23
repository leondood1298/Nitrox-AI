namespace NitroxClient.GameLogic;

/// <summary>
///     What this client does with energy another player took from a source the server says this client simulates, see
///     <see cref="BasePowerSources.DecideForwardedDraw" />.
/// </summary>
public enum ForwardedDraw
{
    /// <summary>
    ///     Nothing to do here: someone else simulates the source by now, or this client cannot do better.
    /// </summary>
    IGNORE,

    /// <summary>
    ///     Take the energy from the source.
    /// </summary>
    APPLY,

    /// <summary>
    ///     The source does not exist on this client: give its simulation back so that someone who has it takes over.
    /// </summary>
    HAND_BACK
}
