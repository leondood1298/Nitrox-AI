using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What the server held for one Scanner Room at one moment.
/// </summary>
/// <param name="Room">
///     A copy that nothing else refers to. It can be read, fingerprinted and serialized without holding anything, and
///     changing it changes nothing on the server.
/// </param>
internal sealed record ScannerRoomSnapshot(MapRoomEntity Room);
