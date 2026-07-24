using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using NitroxClient.GameLogic.Helper;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class BaseDataTest
{
    [TestMethod]
    public void ExactEmptySavePayloadIsValidAndUnoccupied()
    {
        BaseData baseData = new()
        {
            PreCompressionSize = 4,
            Cells = Convert.FromBase64String("Y2EAAA==")
        };

        Assert.IsTrue(baseData.TryHasOccupiedCell(out bool hasOccupiedCell));
        Assert.IsFalse(hasOccupiedCell);
    }

    [TestMethod]
    public void NonZeroCellIsReportedAsOccupied()
    {
        BaseData baseData = CreateBaseData([0, 0, 1, 0]);

        Assert.IsTrue(baseData.TryHasOccupiedCell(out bool hasOccupiedCell));
        Assert.IsTrue(hasOccupiedCell);
    }

    [TestMethod]
    public void MalformedCellPayloadIsRejected()
    {
        BaseData baseData = new()
        {
            PreCompressionSize = 4,
            Cells = [1, 2, 3]
        };

        Assert.IsFalse(baseData.TryHasOccupiedCell(out bool hasOccupiedCell));
        Assert.IsFalse(hasOccupiedCell);
    }

    [TestMethod]
    public void InvalidDeflatePayloadIsRejected()
    {
        BaseData baseData = new()
        {
            PreCompressionSize = 4,
            Cells = [byte.MaxValue]
        };

        Assert.IsFalse(baseData.TryHasOccupiedCell(out bool hasOccupiedCell));
        Assert.IsFalse(hasOccupiedCell);
    }

    [DataTestMethod]
    [DataRow(ushort.MaxValue)]
    [DataRow(ushort.MaxValue + 1)]
    public void ZeroRunsAcrossUShortBoundaryAreValid(int size)
    {
        BaseData baseData = CreateBaseData(new byte[size]);

        Assert.IsTrue(baseData.TryHasOccupiedCell(out bool hasOccupiedCell));
        Assert.IsFalse(hasOccupiedCell);
    }

    [TestMethod]
    public void OccupiedCellAfterUShortBoundaryIsReported()
    {
        byte[] cells = new byte[ushort.MaxValue + 1];
        cells[^1] = 1;
        BaseData baseData = CreateBaseData(cells);

        Assert.IsTrue(baseData.TryHasOccupiedCell(out bool hasOccupiedCell));
        Assert.IsTrue(hasOccupiedCell);
    }

    [TestMethod]
    public void OversizedCellCountIsRejectedBeforeDecompression()
    {
        BaseData baseData = new()
        {
            PreCompressionSize = BaseData.MAX_CELL_COUNT + 1,
            Cells = Convert.FromBase64String("Y2EAAA==")
        };

        Assert.IsFalse(baseData.TryHasOccupiedCell(out bool hasOccupiedCell));
        Assert.IsFalse(hasOccupiedCell);
    }

    [TestMethod]
    public void EmptyBuildWithoutGhostIsStructurallyEmpty()
    {
        BuildEntity buildEntity = BuildEntity.MakeEmpty();
        buildEntity.BaseData = CreateBaseData([0, 0, 0, 0]);

        Assert.IsTrue(buildEntity.TryIsStructurallyEmpty(out bool isStructurallyEmpty));
        Assert.IsTrue(isStructurallyEmpty);
    }

    [TestMethod]
    public void EmptyBuildWithConstructionGhostIsNotStructurallyEmpty()
    {
        BuildEntity buildEntity = BuildEntity.MakeEmpty();
        buildEntity.BaseData = CreateBaseData([0, 0, 0, 0]);
        buildEntity.ChildEntities.Add(GhostEntity.MakeEmpty());

        Assert.IsTrue(buildEntity.TryIsStructurallyEmpty(out bool isStructurallyEmpty));
        Assert.IsFalse(isStructurallyEmpty);
    }

    private static BaseData CreateBaseData(byte[] cells) =>
        new()
        {
            PreCompressionSize = cells.Length,
            Cells = BaseSerializationHelper.CompressBytes(cells)
        };
}
