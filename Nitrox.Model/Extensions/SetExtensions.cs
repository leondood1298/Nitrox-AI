using System;
using System.Buffers;
using System.Collections.Generic;

namespace Nitrox.Model.Extensions;

public static class SetExtensions
{
    /// <summary>
    ///     Calls <paramref name="visitor" /> once for every entry and removes the entries for which it returns true.
    ///     Unlike a RemoveWhere predicate, the visitor is expected to act on the entry, for example by moving it to
    ///     another collection. It must not change the collection being visited: entries are removed after the last visit.
    /// </summary>
    public static void VisitAndRemoveWhere<TParam, T>(this ISet<T> self, TParam parameter, Func<TParam, T, bool> visitor)
    {
        int toRemoveIndex = 0;
        T[] toRemove = ArrayPool<T>.Shared.Rent(self.Count);
        try
        {
            foreach (T item in self)
            {
                if (visitor(parameter, item))
                {
                    toRemove[toRemoveIndex++] = item;
                }
            }
            for (int i = 0; i < toRemoveIndex; i++)
            {
                self.Remove(toRemove[i]);
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(toRemove, true);
        }
    }
}
