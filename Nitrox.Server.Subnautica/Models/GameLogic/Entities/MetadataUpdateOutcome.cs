namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Who is sent what after an entity metadata update, see the members of <see cref="MetadataUpdateResult" />.
/// </summary>
internal enum MetadataUpdateOutcome
{
    ACCEPT,
    ACCEPT_AND_ECHO,
    REJECT,
    REJECT_AND_CORRECT
}
