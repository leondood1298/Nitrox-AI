using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic;

/// <summary>
///     Camera energy is canonical in MapRoomCameraRecord. Runtime camera batteries are therefore not separate prefab children.
/// </summary>
internal static class MapRoomCameraBatteryChildPolicy
{
    internal static bool IsRedundant(bool parentIsMapRoomCamera, EntityMetadata? metadata) =>
        parentIsMapRoomCamera && metadata is BatteryMetadata;
}
