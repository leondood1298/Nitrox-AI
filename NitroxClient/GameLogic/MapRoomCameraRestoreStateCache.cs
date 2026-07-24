using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

/// <summary>
/// Retains authoritative camera state until a loose camera's world object is available.
/// Base pieces and loose world entities are spawned by separate initial-sync pipelines, so
/// either side can legitimately arrive first.
/// </summary>
internal sealed class MapRoomCameraRestoreStateCache
{
    private readonly Dictionary<NitroxId, MapRoomCameraRecord> records = [];
    private readonly Dictionary<NitroxId, MapRoomCameraLight> lights = [];
    private readonly Dictionary<NitroxId, MapRoomCameraComponentState> components = [];
    private readonly HashSet<NitroxId> pendingRecords = [];
    private readonly HashSet<NitroxId> pendingLights = [];
    private readonly HashSet<NitroxId> pendingComponents = [];

    internal void Retain(MapRoomCameraRecord record, bool preferCameraNumber = false)
    {
        if (!records.TryGetValue(record.CameraId, out MapRoomCameraRecord? existing) ||
            preferCameraNumber || IsNewerRestoreRecord(record, existing))
        {
            records[record.CameraId] = new MapRoomCameraRecord(record.CameraId, record.CameraNumber, record.LightOn,
                record.LightRevision, record.Energy, record.Health, record.ComponentRevision);
            pendingRecords.Add(record.CameraId);
        }
        Retain(new MapRoomCameraLight(record.CameraId, record.LightOn, record.LightRevision, true, true));
        Retain(new MapRoomCameraComponentState(record.CameraId, record.Energy, record.Health,
            record.ComponentRevision, true, true));
    }

    private bool IsNewerRestoreRecord(MapRoomCameraRecord candidate, MapRoomCameraRecord existing)
    {
        long knownLightRevision = lights.TryGetValue(candidate.CameraId, out MapRoomCameraLight? light)
            ? System.Math.Max(existing.LightRevision, light.Revision)
            : existing.LightRevision;
        long knownComponentRevision = components.TryGetValue(candidate.CameraId, out MapRoomCameraComponentState? component)
            ? System.Math.Max(existing.ComponentRevision, component.Revision)
            : existing.ComponentRevision;
        return candidate.LightRevision >= knownLightRevision &&
               candidate.ComponentRevision >= knownComponentRevision &&
               (candidate.LightRevision > knownLightRevision || candidate.ComponentRevision > knownComponentRevision);
    }

    internal void Retain(MapRoomCameraLight packet)
    {
        if (!lights.TryGetValue(packet.CameraId, out MapRoomCameraLight existing) || packet.Revision >= existing.Revision)
        {
            lights[packet.CameraId] = packet;
            pendingLights.Add(packet.CameraId);
        }
    }

    internal void Retain(MapRoomCameraComponentState packet)
    {
        if (!components.TryGetValue(packet.CameraId, out MapRoomCameraComponentState existing) || packet.Revision >= existing.Revision)
        {
            components[packet.CameraId] = packet;
            pendingComponents.Add(packet.CameraId);
        }
    }

    internal bool HasKnownState(NitroxId cameraId) =>
        records.ContainsKey(cameraId) || lights.ContainsKey(cameraId) || components.ContainsKey(cameraId);

    internal bool HasPending(NitroxId cameraId) =>
        pendingRecords.Contains(cameraId) || pendingLights.Contains(cameraId) || pendingComponents.Contains(cameraId);

    internal bool MarkPendingForSpawn(NitroxId cameraId)
    {
        if (!HasKnownState(cameraId))
        {
            return false;
        }
        if (records.ContainsKey(cameraId))
        {
            pendingRecords.Add(cameraId);
        }
        if (lights.ContainsKey(cameraId))
        {
            pendingLights.Add(cameraId);
        }
        if (components.ContainsKey(cameraId))
        {
            pendingComponents.Add(cameraId);
        }
        return true;
    }

    internal bool TryTake(NitroxId cameraId, bool cameraAvailable, out PendingCameraState state)
    {
        state = default;
        if (!cameraAvailable)
        {
            return false;
        }

        MapRoomCameraRecord? record = pendingRecords.Remove(cameraId) && records.TryGetValue(cameraId, out MapRoomCameraRecord? retainedRecord)
            ? retainedRecord
            : null;
        MapRoomCameraLight? light = pendingLights.Remove(cameraId) && lights.TryGetValue(cameraId, out MapRoomCameraLight? retainedLight)
            ? retainedLight
            : null;
        MapRoomCameraComponentState? component = pendingComponents.Remove(cameraId) && components.TryGetValue(cameraId, out MapRoomCameraComponentState? retainedComponent)
            ? retainedComponent
            : null;
        if (record == null && light == null && component == null)
        {
            return false;
        }
        state = new PendingCameraState(record, light, component);
        return true;
    }

    internal readonly record struct PendingCameraState(
        MapRoomCameraRecord? Record,
        MapRoomCameraLight? Light,
        MapRoomCameraComponentState? Component);
}
