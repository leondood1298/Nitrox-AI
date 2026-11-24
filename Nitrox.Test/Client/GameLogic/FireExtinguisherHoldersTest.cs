using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class FireExtinguisherHoldersTest
{
    [TestMethod]
    public void EveryGameNamesTheSameTankTheSame()
    {
        NitroxId storedTank = new();

        FireExtinguisherHolders.TankToken(storedTank).Should().Be(FireExtinguisherHolders.TankToken(new NitroxId(storedTank.ToString())));
        FireExtinguisherHolders.TankToken(null).Should().Be(FireExtinguisherHolders.TankToken(null));
    }

    [TestMethod]
    public void ATankPutBackOnTheHolderIsNamedApartFromTheOneTakenBefore()
    {
        // The server remembers a claim on the first tank for a while; the next player must not lose the second to it
        FireExtinguisherHolders.TankToken(new NitroxId()).Should().NotBe(FireExtinguisherHolders.TankToken(null));
        FireExtinguisherHolders.TankToken(new NitroxId()).Should().NotBe(FireExtinguisherHolders.TankToken(new NitroxId()));
    }

    [TestMethod]
    public void AHolderRemembersTheTankLastPutOnItUntilItIsCleared()
    {
        FireExtinguisherHolders holders = new(new ItemSources());
        NitroxId holder = new();
        NitroxId tank = new();

        holders.SetStoredTank(holder, tank);
        holders.GetStoredTank(holder).Should().Be(tank);

        holders.SetStoredTank(holder, null);
        holders.GetStoredTank(holder).Should().BeNull();
    }
}
