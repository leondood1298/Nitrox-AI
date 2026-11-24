using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.GameLogic;

/// <summary>
///     Looks up the game's entity cells, the way <see cref="CellManager.RegisterCellEntity" /> finds the cell of an entity.
/// </summary>
public static class EntityCells
{
    /// <summary>
    ///     True if the cell can hold live entities now: an entity added to it is put in the cell, not set aside as a waiter.
    /// </summary>
    public static bool IsAwake(AbsoluteEntityCell cell)
    {
        return TryGet(cell.BatchId.ToUnity(), cell.CellId.ToUnity(), cell.Level, out EntityCell entityCell) && IsAwake(entityCell.state);
    }

    public static bool IsAwake(EntityCell.State state) => state is EntityCell.State.IsAwake or EntityCell.State.QueuedForSleep;

    /// <summary>
    ///     True for a cell that is waking up after the server was told it is loaded: an entity set aside in it as a waiter is
    ///     put back when the cell is awake, while the server's list of the cell's entities may have been made before the
    ///     entity got there, so it would not be sent again.
    /// </summary>
    public static bool IsWakingAfterLoadWasReported(EntityCell.State state) => state == EntityCell.State.InAwakeAsync;

    /// <summary>
    ///     The cell <paramref name="largeWorldEntity" /> belongs to where it stands now.
    /// </summary>
    public static bool TryGetCellOf(LargeWorldEntity largeWorldEntity, out EntityCell entityCell)
    {
        entityCell = null;
        LargeWorldStreamer streamer = LargeWorldStreamer.main;
        if (!streamer || streamer.cellManager == null)
        {
            return false;
        }
        Int3 block = streamer.GetBlock(largeWorldEntity.transform.position);
        Int3 batchId = block / streamer.blocksPerBatch;
        int level = (int)largeWorldEntity.cellLevel;
        Int3 cellId = block % streamer.blocksPerBatch / BatchCells.GetCellSize(level, streamer.blocksPerBatch);
        return TryGet(batchId, cellId, level, out entityCell);
    }

    private static bool TryGet(Int3 batchId, Int3 cellId, int level, out EntityCell entityCell)
    {
        entityCell = null;
        LargeWorldStreamer streamer = LargeWorldStreamer.main;
        if (!streamer || streamer.cellManager == null ||
            !((Dictionary<Int3, BatchCells>)streamer.cellManager.batch2cells).TryGetValue(batchId, out BatchCells batchCells))
        {
            return false;
        }
        entityCell = batchCells.Get(cellId, level);
        return entityCell != null;
    }
}
