using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What the server held for one Scanner Room camera in the world at one moment, for whoever has to read cameras
///     without holding anything: the camera's entity itself goes on changing.
/// </summary>
/// <param name="State">
///     The camera's light, battery, health, number and dock. A camera's state is one object that is replaced as a
///     whole and never changed, so it is safe to keep.
/// </param>
internal sealed record ScannerCameraSnapshot(NitroxId Id, MapRoomCameraMetadata State);
