using System;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class EntitySpawnedByClient : Packet
{
    public Entity Entity { get; }
    public bool RequireRespawn { get; }
    public bool RequireSimulation { get; }

    /// <summary>
    ///     Set when the game made the entity, an item, from a source (see <see cref="ItemSource" />); the server then
    ///     decides whether this player got it.
    /// </summary>
    public ItemSource? Source { get; }

    public EntitySpawnedByClient(Entity entity, bool requireRespawn = false, bool requireSimulation = true, ItemSource? source = null)
    {
        Entity = entity;
        RequireRespawn = requireRespawn;
        RequireSimulation = requireSimulation;
        Source = source;
    }
}
