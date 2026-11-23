using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What the server held for one Scanner Room at one moment.
/// </summary>
/// <param name="Room">
///     A copy that nothing else refers to. It can be read, fingerprinted and serialized without holding anything, and
///     changing it changes nothing on the server.
/// </param>
/// <param name="ScanResultCount">
///     How many resources the room's scan had found. The results themselves are not part of the room: they are kept
///     in memory only and are reported again by the room's simulating player after a server start.
/// </param>
/// <param name="ScanTypeCount">How many types the room offered to scan for.</param>
internal sealed record ScannerRoomSnapshot(MapRoomEntity Room, int ScanResultCount = 0, int ScanTypeCount = 0);
