using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Extensions;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

public sealed class MapRoomScanTypes(IPacketSender packetSender, SimulationOwnership simulationOwnership)
{
    public void Publish(uGUI_MapRoomScanner scanner)
    {
        MapRoomFunctionality mapRoom = scanner ? scanner.mapRoom : null;
        if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId roomId) || !simulationOwnership.HasAnyLockType(roomId))
        {
            return;
        }
        packetSender.Send(CreateSnapshotPacket(roomId, scanner.availableTechTypes, ResourceTrackerDatabase.GetDetectableTechTypes(),
            mapRoom.wireFrameWorld.position, mapRoom.GetScanRange()));
    }

    public bool ShouldRunVanilla(uGUI_MapRoomScanner scanner)
    {
        MapRoomFunctionality mapRoom = scanner ? scanner.mapRoom : null;
        if (!mapRoom)
        {
            return true;
        }
        if (mapRoom.TryGetNitroxId(out NitroxId roomId) && simulationOwnership.HasAnyLockType(roomId))
        {
            return true;
        }
        MapRoomNetworkState state = mapRoom.GetComponent<MapRoomNetworkState>();
        if (!state || !state.AvailableScanTypesInitialized)
        {
            return true;
        }
        ApplyToUi(scanner, state);
        return false;
    }

    public static void ProcessSnapshot(MapRoomScanTypesSnapshot packet)
    {
        if (!packet.IsServerResponse || !packet.Granted || !NitroxEntity.TryGetObjectFrom(packet.MapRoomId, out GameObject gameObject) || !gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            return;
        }
        ApplySnapshot(mapRoom, packet.Revision, packet.TechTypes);
    }

    public static void ApplySnapshot(MapRoomFunctionality mapRoom, long revision, IEnumerable<NitroxTechType> techTypes)
    {
        MapRoomNetworkState state = mapRoom.gameObject.EnsureComponent<MapRoomNetworkState>();
        if (state.AvailableScanTypesInitialized && revision < state.AvailableScanTypesRevision)
        {
            return;
        }
        state.AvailableScanTypes.Clear();
        foreach (NitroxTechType techType in techTypes)
        {
            state.AvailableScanTypes.Add(techType.ToUnity());
        }
        state.AvailableScanTypesRevision = revision;
        state.AvailableScanTypesInitialized = revision > 0;
        uGUI_MapRoomScanner scanner = mapRoom.GetComponentInChildren<uGUI_MapRoomScanner>(includeInactive: true);
        if (scanner)
        {
            ApplyToUi(scanner, state);
        }
    }

    private static void ApplyToUi(uGUI_MapRoomScanner scanner, MapRoomNetworkState state)
    {
        scanner.availableTechTypes.Clear();
        scanner.availableTechTypes.UnionWith(state.AvailableScanTypes);
        int lastPage = Mathf.Max(0, scanner.numPages - 1);
        scanner.currentPage = Mathf.Clamp(scanner.currentPage, 0, lastPage);
        scanner.RebuildResourceList();
    }

    internal static MapRoomScanTypesSnapshot CreateSnapshotPacket(NitroxId roomId, IEnumerable<TechType> availableTechTypes,
        IEnumerable<TechType> detectableTechTypes, Vector3 scanOrigin, float scanRange) =>
        new(roomId, availableTechTypes.Select(type => type.ToDto()).ToList(), detectableTechTypes.Select(type => type.ToDto()).ToList(),
            scanOrigin.ToDto(), scanRange);
}
