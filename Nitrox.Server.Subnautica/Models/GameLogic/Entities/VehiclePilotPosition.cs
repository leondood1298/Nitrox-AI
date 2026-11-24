using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Where the server keeps a player who pilots a Seamoth or Prawn suit. The vehicle's own position is inside its hull:
///     a player who left the game, or was in it when the server stopped, while piloting came back there, inside the hull
///     without being seated. The game seats a player again from its save (Vehicle.pilotId), which a multiplayer world does
///     not keep per player, and another player may have taken the vehicle meanwhile. The player is kept above the vehicle
///     instead and comes back standing beside it.
/// </summary>
internal static class VehiclePilotPosition
{
    /// <summary>Clear of the hull of either vehicle (the Prawn suit stands about 3 m tall from its centre).</summary>
    internal const float HEIGHT_ABOVE_VEHICLE = 3.5f;

    public static NitroxVector3 BesideVehicle(NitroxVector3 vehiclePosition)
    {
        // World up: water above a vehicle is the likeliest free space, whichever way the vehicle is turned
        return new NitroxVector3(vehiclePosition.X, vehiclePosition.Y + HEIGHT_ABOVE_VEHICLE, vehiclePosition.Z);
    }
}
