using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class MapRoomMetadata : EntityMetadata
{
	[DataMember(Order = 1)]
	public NitroxTechType TypeToScan { get; }

	[DataMember(Order = 2)]
	public int NumNodesScanned { get; }

	[DataMember(Order = 3)]
	public long Generation { get; }

	[DataMember(Order = 4)]
	public long Revision { get; }

	[IgnoreConstructor]
	protected MapRoomMetadata()
	{
	}

	public MapRoomMetadata(NitroxTechType typeToScan, int numNodesScanned, long generation = 0, long revision = 0)
	{
		TypeToScan = typeToScan;
		NumNodesScanned = numNodesScanned;
		Generation = generation;
		Revision = revision;
	}

	public override string ToString()
	{
		return $"[MapRoomMetadata TypeToScan: {TypeToScan} NumNodesScanned: {NumNodesScanned} Generation: {Generation} Revision: {Revision}]";
	}
}




