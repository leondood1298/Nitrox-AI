using NitroxClient.GameLogic.Bases;

namespace Nitrox.Test.Client.GameLogic.Bases;

[TestClass]
public sealed class RoomOccupancyTest
{
    /// <summary>
    ///     A large room takes up two cells side by side; a player in either is inside it.
    /// </summary>
    [TestMethod]
    public void PlayerInAnyCellOfTheRoomIsInside()
    {
        Assert.IsTrue(RoomOccupancy.IsWithin(4, 0, 2, 4, 0, 2, 5, 0, 2));
        Assert.IsTrue(RoomOccupancy.IsWithin(5, 0, 2, 4, 0, 2, 5, 0, 2));
    }

    [TestMethod]
    public void PlayerInTheNextCellIsNotInside()
    {
        Assert.IsFalse(RoomOccupancy.IsWithin(6, 0, 2, 4, 0, 2, 5, 0, 2));
        Assert.IsFalse(RoomOccupancy.IsWithin(4, 1, 2, 4, 0, 2, 5, 0, 2));
        Assert.IsFalse(RoomOccupancy.IsWithin(4, 0, 1, 4, 0, 2, 5, 0, 2));
    }
}
