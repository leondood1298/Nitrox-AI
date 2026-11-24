using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Decides which parts of a Seamoth's or Prawn suit's metadata update the server accepts. The name, colours, lights,
///     health and the facility flag share the vehicle's one metadata slot, and every update carries all of them.
///     <list type="bullet">
///         <item>Any player may rename or recolour a vehicle. A rename carries a higher <see cref="VehicleMetadata.NameVersion" />;
///         a report whose version is not higher (a damage report of the simulating player sent before the rename reached
///         it) keeps the stored name.</item>
///         <item>Health, the facility flag and the lights are only changed in the game of the player who simulates the
///         vehicle (the pilot toggles the lights), so another player's copy of them can be out of date.</item>
///     </list>
/// </summary>
internal static class VehicleMetadataAuthority
{
    public static VehicleMetadata Merge(VehicleMetadata? stored, VehicleMetadata requested, bool senderSimulatesVehicle)
    {
        if (stored == null || stored.GetType() != requested.GetType())
        {
            return requested;
        }

        // A player other than the simulating one only sends on a rename, with one more than the version it knew: an equal
        // version then is a rename that crossed another one, and the later of the two wins as it does for the other players.
        bool takesName = requested.NameVersion > stored.NameVersion || (requested.NameVersion == stored.NameVersion && !senderSimulatesVehicle);
        VehicleMetadata nameSource = takesName ? requested : stored;
        VehicleMetadata stateSource = senderSimulatesVehicle ? requested : stored;

        if (takesName && senderSimulatesVehicle)
        {
            return requested;
        }

        return requested switch
        {
            SeamothMetadata => new SeamothMetadata(((SeamothMetadata)stateSource).LightsOn, stateSource.Health, stateSource.InPrecursor, nameSource.Name, nameSource.Colors, nameSource.NameVersion),
            ExosuitMetadata => new ExosuitMetadata(stateSource.Health, stateSource.InPrecursor, nameSource.Name, nameSource.Colors, nameSource.NameVersion),
            _ => requested
        };
    }
}
