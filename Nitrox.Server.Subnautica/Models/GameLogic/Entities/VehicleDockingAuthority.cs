using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Decides whether a vehicle may dock into or leave a docking bay (a moonpool's or a Cyclops'). A bay holds one
///     vehicle. Two vehicles entering one bay at the same moment both dock in their own games, as each game sees the bay
///     free until it hears of the other; the server keeps the first and sends the second one out again.
/// </summary>
internal static class VehicleDockingAuthority
{
    public enum Decision
    {
        ACCEPT,

        /// <summary>The sender does not simulate the vehicle: nothing changes and nobody is told.</summary>
        IGNORE,

        /// <summary>The bay already holds another vehicle: the sender's game must undock it again.</summary>
        SEND_OUT
    }

    public static Decision MayDock(Entity vehicle, Entity bay, bool senderSimulatesVehicle)
    {
        if (!senderSimulatesVehicle)
        {
            return Decision.IGNORE;
        }
        return bay.ChildEntities.Any(child => child is VehicleEntity && !child.Id.Equals(vehicle.Id)) ? Decision.SEND_OUT : Decision.ACCEPT;
    }

    /// <summary>
    ///     A vehicle leaves a bay only when it is docked there and the player taking it out simulates it (it asked for the
    ///     vehicle's lock before undocking).
    /// </summary>
    public static bool MayUndock(Entity vehicle, Entity? bay, bool senderSimulatesVehicle)
    {
        return senderSimulatesVehicle && bay != null && bay.Id.Equals(vehicle.ParentId);
    }
}
