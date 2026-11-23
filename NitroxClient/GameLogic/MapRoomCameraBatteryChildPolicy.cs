using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic;

/// <summary>
///     A camera's energy is part of the camera's own metadata. Its battery is therefore not an entity of its own.
/// </summary>
internal static class MapRoomCameraBatteryChildPolicy
{
    internal static bool IsRedundant(bool parentIsMapRoomCamera, EntityMetadata? metadata) =>
        parentIsMapRoomCamera && metadata is BatteryMetadata;
}
