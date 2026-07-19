using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Produces a deterministic digest of the canonical Scanner Room state used for multiplayer synchronization.
///     The canonical text intentionally excludes rendering and transient movement state.
/// </summary>
internal static class ScannerRoomStateFingerprint
{
    private static readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;

    public static ScannerRoomStateSnapshot Create(MapRoomEntity room)
    {
        ArgumentNullException.ThrowIfNull(room);

        lock (room)
        {
            return CreateLocked(room);
        }
    }

    public static string? Validate(MapRoomEntity room)
    {
        ArgumentNullException.ThrowIfNull(room);
        lock (room)
        {
            if (room.DockingRevision < 0 || room.ScanResultGeneration < 0 || room.ScanResultRevision < 0 ||
                room.AvailableScanTypesRevision < 0)
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
            if (records.Length != (room.CameraRegistry?.Count ?? 0) ||
                records.Select(record => record.CameraId).Distinct().Count() != records.Length)
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
                lock (record)
                {
                    if (!float.IsFinite(record.Energy) || record.Energy is < 0f or > MapRoomCameraRecord.MAX_ENERGY ||
                        !float.IsFinite(record.Health) || record.Health is < 0f or > MapRoomCameraRecord.MAX_HEALTH ||
                        record.LightRevision < 0 || record.ComponentRevision < 0)
                    {
                        return "invalid_camera_component";
                    }
                }
            }

            if (room.Metadata is MapRoomMetadata metadata && (metadata.Generation < 0 || metadata.Revision < 0 || metadata.NumNodesScanned < 0))
            {
                return "invalid_scan_metadata";
            }
            return null;
        }
    }

    private static ScannerRoomStateSnapshot CreateLocked(MapRoomEntity room)
    {

        MapRoomCameraRecord?[] cameras = room.CameraRegistry?.Select(static camera => (MapRoomCameraRecord?)camera).ToArray() ?? [];
        MapRoomScanResultRecord?[] scanResults = room.ScanResults?.Select(static result => (MapRoomScanResultRecord?)result).ToArray() ?? [];
        string?[] scanTypes = room.AvailableScanTypes?.Select(static type => type?.Name).ToArray() ?? [];
        MapRoomMetadata? metadata = room.Metadata as MapRoomMetadata;

        StringBuilder canonical = new(512);
        AppendField(canonical, "version", "1");
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
        AppendField(canonical, "metadataRevision", Format(metadata?.Revision ?? 0));

        AppendField(canonical, "scanGeneration", Format(room.ScanResultGeneration));
        AppendField(canonical, "scanRevision", Format(room.ScanResultRevision));
        AppendField(canonical, "scanResultCount", Format(scanResults.Length));
        foreach (string scanResult in scanResults.Select(CanonicalScanResult).Order(StringComparer.Ordinal))
        {
            AppendField(canonical, "scanResult", scanResult);
        }

        AppendField(canonical, "scanTypesRevision", Format(room.AvailableScanTypesRevision));
        AppendField(canonical, "scanTypeCount", Format(scanTypes.Length));
        foreach (string? scanType in scanTypes.Order(StringComparer.Ordinal))
        {
            AppendField(canonical, "scanType", scanType);
        }

        AppendField(canonical, "fabricator", CanonicalCrafter(room.FabricatorMetadata));

        string canonicalState = canonical.ToString();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalState));
        string fingerprint = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return new ScannerRoomStateSnapshot(fingerprint, canonicalState, cameras.Length, scanResults.Length, scanTypes.Length,
            room.DockingRevision, metadata?.TypeToScan?.Name ?? "", metadata?.NumNodesScanned ?? 0,
            metadata?.Generation ?? 0, metadata?.Revision ?? 0, room.FabricatorMetadata != null);
    }

    private static string CanonicalCamera(MapRoomCameraRecord? camera)
    {
        if (camera is null)
        {
            return "null";
        }

        lock (camera)
        {
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

    private static string CanonicalScanResult(MapRoomScanResultRecord? result)
    {
        if (result is null)
        {
            return "null";
        }

        StringBuilder canonical = new(160);
        AppendField(canonical, "resource", result.ResourceId);
        AppendField(canonical, "type", result.TechType?.Name);
        AppendField(canonical, "x", Format(result.Position.X));
        AppendField(canonical, "y", Format(result.Position.Y));
        AppendField(canonical, "z", Format(result.Position.Z));
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
    int ScanResultCount,
    int ScanTypeCount,
    long DockingRevision,
    string ScanTarget,
    int NodesScanned,
    long MetadataGeneration,
    long MetadataRevision,
    bool FabricatorActive);
