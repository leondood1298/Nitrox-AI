using Nitrox.Model.Logger;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Extensions;
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
		if (!gameObject.TryGetComponent<MapRoomFunctionality>(out var component))
		{
			Log.Error("Could not find MapRoomFunctionality on " + gameObject.name);
			return;
		}
		TechType techType = metadata.TypeToScan.ToUnity();
		bool flag = component.typeToScan != techType;
		MapRoomNetworkState state = gameObject.EnsureComponent<MapRoomNetworkState>();
		if (metadata.Generation < state.Generation || metadata.Revision < state.Revision)
		{
			Log.Warn($"Ignoring stale Map Room metadata for {gameObject.name}: incoming generation/revision {metadata.Generation}/{metadata.Revision}, current {state.Generation}/{state.Revision}");
			return;
		}
		using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
		{
			if (flag)
			{
				component.StartScanning(techType);
			}
			component.numNodesScanned = metadata.NumNodesScanned;
		}
		state.Generation = metadata.Generation;
		state.Revision = metadata.Revision;
		if (flag)
		{
			uGUI_MapRoomScanner componentInChildren = component.GetComponentInChildren<uGUI_MapRoomScanner>(includeInactive: true);
			if ((bool)componentInChildren)
			{
				componentInChildren.UpdateGUIState();
			}
		}
	}
}


