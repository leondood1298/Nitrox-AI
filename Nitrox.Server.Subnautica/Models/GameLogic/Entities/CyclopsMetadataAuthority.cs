using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Decides which parts of a Cyclops metadata update the server accepts. Any player may press the Cyclops' buttons
///     (silent running, shield, sonar, engine), and each press sends the whole metadata, hull health included. Health
///     and destruction are only simulated by the player holding the Cyclops' simulation lock, so another player's copy
///     of them can be out of date and must not overwrite the stored values.
/// </summary>
internal static class CyclopsMetadataAuthority
{
    public static CyclopsMetadata Merge(CyclopsMetadata? stored, CyclopsMetadata requested, bool senderSimulatesCyclops)
    {
        if (senderSimulatesCyclops || stored == null)
        {
            return requested;
        }
        return new CyclopsMetadata(requested.SilentRunningOn, requested.ShieldOn, requested.SonarOn, requested.EngineOn, requested.EngineMode,
            stored.Health, stored.IsDestroyed);
    }
}
