using System;
using System.Collections.Generic;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public sealed class BuildingResync : Packet
{
    public Dictionary<BuildEntity, int> BuildEntities { get; }
    public Dictionary<ModuleEntity, int> ModuleEntities { get; }

    /// <summary>
    ///     True when only the given entities are resynced (one base): everything else the player has stays as it is.
    ///     False when these are all the bases and outdoor modules, and any other is removed.
    /// </summary>
    public bool Partial { get; }

    public BuildingResync(Dictionary<BuildEntity, int> buildEntities, Dictionary<ModuleEntity, int> moduleEntities, bool partial)
    {
        BuildEntities = buildEntities;
        ModuleEntities = moduleEntities;
        Partial = partial;
    }
}
