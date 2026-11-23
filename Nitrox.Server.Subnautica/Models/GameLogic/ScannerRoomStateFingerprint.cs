using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Produces a deterministic digest of the Scanner Room state that the server saves and sends to joining players:
///     the cameras in its docks with their state, the scan target with its progress, and what its fabricator is
///     crafting. Two rooms with the same digest are saved identically. What a scan found and the types it offers are left out on purpose: the server only holds
///     them in memory, and a digest that changed with them would differ between a save and the same save loaded again.
/// </summary>
/// <remarks>
///     The room handed in must not change while it is read: it is either the copy in a
///     <see cref="ScannerRoomSnapshot" />, or the live room read by <see cref="ScannerRoomManager" /> inside its gate.
/// </remarks>
internal static class ScannerRoomStateFingerprint
{
    private static readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;

    public static string? Validate(MapRoomEntity room)
    {
        ArgumentNullException.ThrowIfNull(room);
        MapRoomCameraMetadata[] cameras = GetCameras(room).Select(ReadState).ToArray();
        if (cameras.Any(camera => camera.DockingIndex is < 0 or >= MapRoomCameraMetadata.DOCK_COUNT))
        {
            return "child_camera_without_dock";
        }
        if (cameras.Select(camera => camera.DockingIndex).Distinct().Count() != cameras.Length)
        {
            return "duplicate_dock";
        }
        if (cameras.Any(camera => camera.CameraNumber <= 0) || cameras.Select(camera => camera.CameraNumber).Distinct().Count() != cameras.Length)
        {
            return "invalid_camera_number";
        }
        foreach (MapRoomCameraMetadata camera in cameras)
        {
            if (!float.IsFinite(camera.Energy) || camera.Energy is < 0f or > MapRoomCameraMetadata.MAX_ENERGY ||
                !float.IsFinite(camera.Health) || camera.Health is < 0f or > MapRoomCameraMetadata.MAX_HEALTH)
            {
                return "invalid_camera_component";
            }
        }

        if (room.Metadata is MapRoomMetadata metadata && (metadata.Generation < 0 || metadata.NumNodesScanned < 0))
        {
            return "invalid_scan_metadata";
        }
        return null;
    }

    public static ScannerRoomStateSnapshot Create(MapRoomEntity room)
    {
        ArgumentNullException.ThrowIfNull(room);

        Entity[] cameras = GetCameras(room);
        MapRoomMetadata? metadata = room.Metadata as MapRoomMetadata;

        StringBuilder canonical = new(512);
        AppendField(canonical, "version", "5");
        AppendField(canonical, "room", FormatId(room.Id));
        AppendField(canonical, "parent", FormatId(room.ParentId));
        AppendField(canonical, "cellX", Format(room.Cell.X));
        AppendField(canonical, "cellY", Format(room.Cell.Y));
        AppendField(canonical, "cellZ", Format(room.Cell.Z));
        AppendField(canonical, "cameraCount", Format(cameras.Length));

        foreach (string camera in cameras.Select(CanonicalCamera).Order(StringComparer.Ordinal))
        {
            AppendField(canonical, "camera", camera);
        }

        AppendField(canonical, "target", metadata?.TypeToScan?.Name);
        AppendField(canonical, "nodesScanned", Format(metadata?.NumNodesScanned ?? 0));
        AppendField(canonical, "metadataGeneration", Format(metadata?.Generation ?? 0));
        CrafterMetadata? crafting = GetFabricatorState(room);
        AppendField(canonical, "fabricator", CanonicalCrafter(crafting));

        string canonicalState = canonical.ToString();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalState));
        string fingerprint = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return new ScannerRoomStateSnapshot(fingerprint, canonicalState, cameras.Length, metadata?.TypeToScan?.Name ?? "", metadata?.NumNodesScanned ?? 0,
            metadata?.Generation ?? 0, crafting is { Amount: > 0 }, GetDockedCameraId(cameras, 0), GetDockedCameraId(cameras, 1));
    }

    /// <summary>
    ///     The cameras in the room's docks: the children of the room that are cameras.
    /// </summary>
    public static Entity[] GetCameras(MapRoomEntity room) => room.ChildEntities?.Where(ScannerRoomManager.IsCamera).ToArray() ?? [];

    /// <summary>
    ///     What the room's fabricator is crafting. The fabricator is the child of the room that is found by its path
    ///     in the room's game object.
    /// </summary>
    public static CrafterMetadata? GetFabricatorState(MapRoomEntity room) =>
        room.ChildEntities?.OfType<PathBasedChildEntity>().Select(part => part.Metadata).OfType<CrafterMetadata>().FirstOrDefault();

    public static MapRoomCameraMetadata ReadState(Entity camera) => camera.Metadata as MapRoomCameraMetadata ?? new MapRoomCameraMetadata();

    private static NitroxId? GetDockedCameraId(Entity[] cameras, int dockingIndex) =>
        cameras.FirstOrDefault(camera => ReadState(camera).DockingIndex == dockingIndex)?.Id;

    private static string CanonicalCamera(Entity camera)
    {
        MapRoomCameraMetadata state = ReadState(camera);
        StringBuilder canonical = new(160);
        AppendField(canonical, "id", FormatId(camera.Id));
        AppendField(canonical, "dock", Format(state.DockingIndex));
        AppendField(canonical, "number", Format(state.CameraNumber));
        AppendField(canonical, "light", state.LightOn ? "1" : "0");
        AppendField(canonical, "energy", Format(state.Energy));
        AppendField(canonical, "health", Format(state.Health));
        return canonical.ToString();
    }

    private static string CanonicalCrafter(CrafterMetadata? metadata)
    {
        if (metadata is null)
        {
            return "";
        }
        StringBuilder canonical = new(96);
        AppendField(canonical, "type", metadata.TechType?.Name);
        AppendField(canonical, "start", Format(metadata.StartTime));
        AppendField(canonical, "duration", Format(metadata.Duration));
        AppendField(canonical, "amount", Format(metadata.Amount));
        AppendField(canonical, "linked", Format(metadata.LinkedIndex));
        return canonical.ToString();
    }

    private static void AppendField(StringBuilder builder, string name, string? value)
    {
        value ??= "";
        builder.Append(name);
        builder.Append('=');
        builder.Append(value.Length.ToString(invariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }

    private static string FormatId(NitroxId? id) => id?.ToString() ?? "";
    private static string Format(int value) => value.ToString(invariantCulture);
    private static string Format(long value) => value.ToString(invariantCulture);
    private static string Format(float value) => value.ToString("R", invariantCulture);
}

internal readonly record struct ScannerRoomStateSnapshot(
    string Fingerprint,
    string CanonicalState,
    int CameraCount,
    string ScanTarget,
    int NodesScanned,
    long MetadataGeneration,
    bool FabricatorActive,
    NitroxId? LeftDockCameraId = null,
    NitroxId? RightDockCameraId = null);
