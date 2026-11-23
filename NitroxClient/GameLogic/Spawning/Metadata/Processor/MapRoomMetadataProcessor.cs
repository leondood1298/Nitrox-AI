using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class MapRoomMetadataProcessor : EntityMetadataProcessor<MapRoomMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, MapRoomMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            Log.Error($"Could not find MapRoomFunctionality on {gameObject.name}");
            return;
        }
        TechType techType = metadata.TypeToScan.ToUnity();
        bool targetChanged = mapRoom.typeToScan != techType;
        MapRoomNetworkState state = gameObject.EnsureComponent<MapRoomNetworkState>();
        if (metadata.Generation < state.Generation || metadata.Revision < state.Revision)
        {
            Log.Warn($"Ignoring stale Map Room metadata for {gameObject.name}: incoming generation/revision {metadata.Generation}/{metadata.Revision}, current {state.Generation}/{state.Revision}");
            return;
        }
        bool applyScanningState = ShouldApplyScanningState(state.MetadataInitialized, targetChanged);
        bool generationAdvanced = metadata.Generation > state.Generation;
        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        {
            if (applyScanningState)
            {
                mapRoom.StartScanning(techType);
            }
            mapRoom.numNodesScanned = metadata.NumNodesScanned;
        }
        state.MetadataInitialized = true;
        if (generationAdvanced)
        {
            state.ResultStateInitialized = false;
        }
        state.Generation = metadata.Generation;
        state.Revision = metadata.Revision;
        MapRoomScanResults.RefreshResultConsumers(mapRoom);
        if (applyScanningState)
        {
            uGUI_MapRoomScanner scanner = mapRoom.GetComponentInChildren<uGUI_MapRoomScanner>(includeInactive: true);
            if (scanner)
            {
                scanner.UpdateGUIState();
            }
        }
    }

    internal static bool ShouldApplyScanningState(bool metadataInitialized, bool targetChanged) =>
        !metadataInitialized || targetChanged;
}
