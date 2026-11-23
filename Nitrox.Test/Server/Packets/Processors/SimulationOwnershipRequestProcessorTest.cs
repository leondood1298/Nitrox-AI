using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Test.Server.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A lock is asked for in the same way for every entity and every purpose. What a lock on a Scanner Room camera
///     means beyond that is tested with <c>ScannerRoomManager</c>.
/// </summary>
[TestClass]
public sealed class SimulationOwnershipRequestProcessorTest
{
    [TestMethod]
    public async Task LockRequestForSomethingThatIsNotAWorldEntityIsStillGranted()
    {
        // The fabricator in a Cyclops or lifepod is a child entity. Players take turns on it with an exclusive lock.
        ScannerRoomScenarioFixture scenario = new();
        NitroxId fabricatorId = new("00000000-0000-0000-0000-000000000901");

        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, fabricatorId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        Assert.IsFalse((await scenario.RequestOwnershipAsync(scenario.PlayerB, fabricatorId, SimulationLockType.EXCLUSIVE)).LockAcquired,
            "The second player has to wait.");
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, fabricatorId, SimulationLockType.TRANSIENT)).LockAcquired);
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, fabricatorId, SimulationLockType.EXCLUSIVE)).LockAcquired,
            "A transient lock held by someone else does not block an exclusive request.");
    }
}
