using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     The smell another player leaves on an item they drop, as the game that simulates a Stalker remembers it. What the
///     Stalker does with it needs the game.
/// </summary>
[TestClass]
public sealed class StalkerFriendshipsTest
{
    private const float DROP_STRENGTH = 1f;
    private const float DROP_FALLOFF = 0.05f;

    private readonly NitroxId itemId = new();
    private readonly NitroxId playerId = new();
    private readonly StalkerFriendships friendships;

    public StalkerFriendshipsTest()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation((SessionId)(ushort)1));
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        friendships = new StalkerFriendships(packetSender, new SimulationOwnership(multiplayerSession, packetSender), new StalkerMetadataExtractor());
    }

    [TestMethod]
    public void ItemSmellsOfThePlayerWhoDroppedItAsMuchAsItWouldOnTheirGame()
    {
        friendships.RememberSmell(itemId, playerId, DROP_STRENGTH, DROP_FALLOFF, 100f);

        Assert.IsTrue(friendships.TryGetSmell(itemId, 110f, out NitroxId owner, out float strength, out float falloff));
        Assert.AreEqual(playerId, owner);
        Assert.AreEqual(0.5f, strength, 0.0001f, "Smell.Update takes the falloff from the strength every second.");
        Assert.AreEqual(DROP_FALLOFF, falloff);
    }

    [TestMethod]
    public void SmellIsGoneOnceItHasFaded()
    {
        friendships.RememberSmell(itemId, playerId, DROP_STRENGTH, DROP_FALLOFF, 100f);

        Assert.IsFalse(friendships.TryGetSmell(itemId, 121f, out _, out _, out _));
    }

    [TestMethod]
    public void ItemNobodyElseDroppedHasNoSmellFromThisClass()
    {
        Assert.IsFalse(friendships.TryGetSmell(itemId, 100f, out _, out _, out _));
    }

    [TestMethod]
    public void FadedSmellsAreNotKept()
    {
        NitroxId otherItemId = new();
        friendships.RememberSmell(itemId, playerId, DROP_STRENGTH, DROP_FALLOFF, 0f);

        friendships.RememberSmell(otherItemId, playerId, DROP_STRENGTH, DROP_FALLOFF, 1000f);

        Assert.IsFalse(friendships.TryGetSmell(itemId, 0f, out _, out _, out _), "Dropped when the next smell came in long after it faded.");
        Assert.IsTrue(friendships.TryGetSmell(otherItemId, 1000f, out _, out _, out _));
    }

    [TestMethod]
    [DataRow(BehaviourType.SmallFish, 1f)]
    [DataRow(BehaviourType.MediumFish, 1.5f)]
    [DataRow(BehaviourType.Shark, 3f)]
    public void FishFeedsAsMuchAsInMeleeAttackTryEat(BehaviourType fishType, float foodValue)
    {
        Assert.AreEqual(foodValue, StalkerFriendships.GetFoodValue(fishType));
    }
}
