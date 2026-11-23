using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Produces a deterministic digest of the Scanner Room state that the server saves and sends to joining players:
///     the docks, the cameras, the scan target with its progress, and the fabricator. Two rooms with the same digest
///     are saved identically. What a scan found and the types it offers are left out on purpose: the server only holds
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
        if (room.DockingRevision < 0)
        {
            return "negative_revision";
        }

        NitroxId?[] docked = [room.LeftDockCameraId, room.RightDockCameraId];
        NitroxId[] dockedIds = docked.Where(id => id != null).Select(id => id!).ToArray();
        if (dockedIds.Distinct().Count() != dockedIds.Length)
        {
            return "duplicate_dock";
        }

        MapRoomCameraRecord[] records = room.CameraRegistry?.Where(record => record != null).ToArray() ?? [];
        if (records.Length != (room.CameraRegistry?.Count ?? 0))
        {
            return "null_camera_record";
        }
        if (records.Select(record => record.CameraId).Distinct().Count() != records.Length)
        {
            return "duplicate_camera_registration";
        }
        if (records.Any(record => record.CameraNumber <= 0) ||
            records.Select(record => record.CameraNumber).Distinct().Count() != records.Length)
        {
            return "invalid_camera_number";
        }
        if (dockedIds.Any(id => records.All(record => record.CameraId != id)))
        {
            return "dock_without_registration";
        }

        foreach (MapRoomCameraRecord record in records)
        {
            if (!float.IsFinite(record.Energy) || record.Energy is < 0f or > MapRoomCameraRecord.MAX_ENERGY ||
                !float.IsFinite(record.Health) || record.Health is < 0f or > MapRoomCameraRecord.MAX_HEALTH ||
                record.LightRevision < 0 || record.ComponentRevision < 0)
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

        MapRoomCameraRecord?[] cameras = room.CameraRegistry?.Select(static camera => (MapRoomCameraRecord?)camera).ToArray() ?? [];
        MapRoomMetadata? metadata = room.Metadata as MapRoomMetadata;

        StringBuilder canonical = new(512);
        AppendField(canonical, "version", "2");
        AppendField(canonical, "room", FormatId(room.Id));
        AppendField(canonical, "parent", FormatId(room.ParentId));
        AppendField(canonical, "cellX", Format(room.Cell.X));
        AppendField(canonical, "cellY", Format(room.Cell.Y));
        AppendField(canonical, "cellZ", Format(room.Cell.Z));
        AppendField(canonical, "leftDock", FormatId(room.LeftDockCameraId));
        AppendField(canonical, "rightDock", FormatId(room.RightDockCameraId));
        AppendField(canonical, "dockingRevision", Format(room.DockingRevision));
        AppendField(canonical, "cameraCount", Format(cameras.Length));

        foreach (string camera in cameras.Select(CanonicalCamera).Order(StringComparer.Ordinal))
        {
            AppendField(canonical, "camera", camera);
        }

        AppendField(canonical, "target", metadata?.TypeToScan?.Name);
        AppendField(canonical, "nodesScanned", Format(metadata?.NumNodesScanned ?? 0));
        AppendField(canonical, "metadataGeneration", Format(metadata?.Generation ?? 0));
        AppendField(canonical, "fabricator", CanonicalCrafter(room.FabricatorMetadata));

        string canonicalState = canonical.ToString();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalState));
        string fingerprint = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return new ScannerRoomStateSnapshot(fingerprint, canonicalState, cameras.Length, room.DockingRevision, metadata?.TypeToScan?.Name ?? "",
            metadata?.NumNodesScanned ?? 0, metadata?.Generation ?? 0, room.FabricatorMetadata != null, room.LeftDockCameraId, room.RightDockCameraId);
    }

    private static string CanonicalCamera(MapRoomCameraRecord? camera)
    {
        if (camera is null)
        {
            return "null";
        }

        StringBuilder canonical = new(160);
        AppendField(canonical, "id", FormatId(camera.CameraId));
        AppendField(canonical, "number", Format(camera.CameraNumber));
        AppendField(canonical, "light", camera.LightOn ? "1" : "0");
        AppendField(canonical, "lightRevision", Format(camera.LightRevision));
        AppendField(canonical, "energy", Format(camera.Energy));
        AppendField(canonical, "health", Format(camera.Health));
        AppendField(canonical, "componentRevision", Format(camera.ComponentRevision));
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
    long DockingRevision,
    string ScanTarget,
    int NodesScanned,
    long MetadataGeneration,
    bool FabricatorActive,
    NitroxId? LeftDockCameraId = null,
    NitroxId? RightDockCameraId = null);
