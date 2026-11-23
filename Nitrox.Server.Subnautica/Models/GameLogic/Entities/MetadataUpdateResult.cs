using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     What the server does with an entity metadata update once the manager that owns that kind of state has looked at
///     it. The manager has already stored what it accepted; the packet processor only sends.
/// </summary>
internal readonly record struct MetadataUpdateResult(MetadataUpdateOutcome Outcome, EntityMetadata? Metadata)
{
    /// <summary>
    ///     Nothing is stored and nobody is told.
    /// </summary>
    public static MetadataUpdateResult Reject { get; } = new(MetadataUpdateOutcome.REJECT, null);

    /// <summary>
    ///     The other players are told. The sender already holds this state, and sending it back would undo what its
    ///     simulation did during the round trip.
    /// </summary>
    public static MetadataUpdateResult Accept(EntityMetadata accepted) => new(MetadataUpdateOutcome.ACCEPT, accepted);

    /// <summary>
    ///     Everyone including the sender is told, because the server stored something the sender does not have (it
    ///     added or changed a value).
    /// </summary>
    public static MetadataUpdateResult AcceptAndEcho(EntityMetadata accepted) => new(MetadataUpdateOutcome.ACCEPT_AND_ECHO, accepted);

    /// <summary>
    ///     Nothing is stored. The sender applied a state locally that it was not allowed to set, so it gets the stored
    ///     one back; otherwise it would keep showing its own.
    /// </summary>
    public static MetadataUpdateResult RejectAndCorrect(EntityMetadata stored) => new(MetadataUpdateOutcome.REJECT_AND_CORRECT, stored);
}
