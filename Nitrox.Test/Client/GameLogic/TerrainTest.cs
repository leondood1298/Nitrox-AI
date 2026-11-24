using NitroxClient.Communication.Abstract;
using NSubstitute;
using Terrain = NitroxClient.GameLogic.Terrain;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     What the next visibility packet will report, as cells load and unload between two packets.
/// </summary>
[TestClass]
public sealed class TerrainTest
{
    private static readonly Int3 batch = new(10, 18, 10);
    private static readonly Int3 cell = new(1, 2, 3);

    [TestMethod]
    public void ACellThatCameAndWentBeforeBeingReportedIsNotReportedAtAll()
    {
        Terrain terrain = new(Substitute.For<IMultiplayerSession>(), Substitute.For<IPacketSender>());

        terrain.CellLoaded(batch, cell, 0);
        terrain.CellUnloaded(batch, cell, 0);

        terrain.PendingAddedCells.Should().BeEmpty("the server would generate and send the entities of a cell this game no longer has");
        terrain.PendingRemovedCells.Should().BeEmpty();
    }

    [TestMethod]
    public void ACellThatUnloadsAndLoadsAgainIsReportedAsAdded()
    {
        Terrain terrain = new(Substitute.For<IMultiplayerSession>(), Substitute.For<IPacketSender>());
        terrain.CellLoaded(batch, cell, 0);
        terrain.CellUnloaded(batch, cell, 0);

        terrain.CellLoaded(batch, cell, 0);

        terrain.PendingAddedCells.Should().ContainSingle();
        terrain.PendingRemovedCells.Should().BeEmpty();
    }
}
