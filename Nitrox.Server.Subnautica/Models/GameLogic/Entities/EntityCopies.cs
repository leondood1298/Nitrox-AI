using System.Collections.Generic;
using System.Linq;
using BinaryPack;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Copies of entity hierarchies, to read or send while packet processors keep changing the live ones.
/// </summary>
/// <remarks>
///     The first sync was serialized from the live entities. The serializer reads a list's count and then its items
///     without any check, so an item moved out of a locker meanwhile could reach the joining game as a missing (null)
///     child, which stopped the spawning of everything after it in that base. A copy is made the same way and checked;
///     a torn one is made again.
/// </remarks>
internal static class EntityCopies
{
    private const int ATTEMPTS = 3;

    /// <summary>
    ///     A deep copy of <paramref name="entities" /> and everything below them.
    /// </summary>
    /// <param name="torn">True if every attempt read a hierarchy that was being changed; the last copy, its missing children left out, is returned.</param>
    public static List<T> Copy<T>(IReadOnlyList<T> entities, out bool torn) where T : Entity
    {
        List<Entity>? copy = null;
        for (int attempt = 1; attempt <= ATTEMPTS; attempt++)
        {
            try
            {
                Holder read = BinaryConverter.Deserialize<Holder>(BinaryConverter.Serialize(new Holder { Entities = [.. entities] }));
                copy = read.Entities ?? [];
                if (IsWhole(copy))
                {
                    torn = false;
                    return copy.Cast<T>().ToList();
                }
            }
            catch (Exception) when (attempt < ATTEMPTS)
            {
                // A collection resized under the serializer; the next attempt normally reads it whole
            }
        }

        torn = true;
        copy ??= [];
        RemoveMissing(copy);
        return copy.Cast<T>().ToList();
    }

    internal static bool IsWhole(IEnumerable<Entity?> entities)
    {
        foreach (Entity? entity in entities)
        {
            if (entity == null || entity.ChildEntities == null || !IsWhole(entity.ChildEntities))
            {
                return false;
            }
        }
        return true;
    }

    private static void RemoveMissing(List<Entity> entities)
    {
        entities.RemoveAll(entity => entity == null);
        foreach (Entity entity in entities)
        {
            entity.ChildEntities ??= [];
            RemoveMissing(entity.ChildEntities);
        }
    }

    public sealed class Holder
    {
        public List<Entity>? Entities { get; set; }
    }
}
