using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.Helpers;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CreatureMetadataUpdatesTest
{
    private static readonly NitroxVector3 leash = new(4f, -60f, 12f);

    [TestMethod]
    public void AnInfectionAfterALeashKeepsTheLeash()
    {
        CreatureInfectionMetadata leashUpdate = (CreatureInfectionMetadata)new CreatureInfectionMetadata(CreatureMetadata.UNDECIDED).WithLeash(leash);

        CreatureInfectionMetadata merged = CreatureMetadataUpdates.Merge(leashUpdate, new CreatureInfectionMetadata(0.4f));

        Assert.AreEqual(0.4f, merged.InfectedAmount);
        Assert.IsTrue(merged.HasLeash);
        Assert.AreEqual(leash, merged.LeashPosition);
    }

    [TestMethod]
    public void ALeashAfterAnInfectionKeepsTheInfectionAndSize()
    {
        CreatureInfectionMetadata infection = (CreatureInfectionMetadata)new CreatureInfectionMetadata(0.4f, 900f).WithSize(0.6f);

        CreatureInfectionMetadata merged = CreatureMetadataUpdates.Merge(infection, (CreatureInfectionMetadata)new CreatureInfectionMetadata(CreatureMetadata.UNDECIDED).WithLeash(leash));

        Assert.AreEqual(0.4f, merged.InfectedAmount);
        Assert.AreEqual(900f, merged.EnzymeEndTime);
        Assert.AreEqual(0.6f, merged.Size);
        Assert.AreEqual(leash, merged.LeashPosition);
    }

    [TestMethod]
    public void TheNewerValueOfAFieldWins()
    {
        NitroxVector3 newerLeash = new(1f, 2f, 3f);
        CreatureInfectionMetadata older = (CreatureInfectionMetadata)new CreatureInfectionMetadata(0.4f).WithLeash(leash);
        CreatureInfectionMetadata newer = (CreatureInfectionMetadata)new CreatureInfectionMetadata(0.1f).WithLeash(newerLeash);

        CreatureInfectionMetadata merged = CreatureMetadataUpdates.Merge(older, newer);

        Assert.AreEqual(0.1f, merged.InfectedAmount);
        Assert.AreEqual(newerLeash, merged.LeashPosition);
    }

    [TestMethod]
    public void OtherMetadataIsReplacedAsBefore()
    {
        NitroxId id = new();
        EntityMetadataUpdate next = new(id, new StalkerMetadata(Optional.Empty, 0d, CreatureMetadata.UNDECIDED));

        Assert.AreSame(next, CreatureMetadataUpdates.Merge(new EntityMetadataUpdate(id, new CreatureInfectionMetadata(0.4f)), next));
    }

    [TestMethod]
    public void ThrottledInfectionAndLeashUpdatesOfACreatureAreBothSent()
    {
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        packetSender.Send(Arg.Any<EntityMetadataUpdate>()).Returns(true);
        ThrottledPacketSender throttled = new(packetSender);
        NitroxId id = new();

        // The first goes at once, the next two wait for the throttle and would have replaced each other
        throttled.SendThrottled(new EntityMetadataUpdate(id, new CreatureInfectionMetadata(0.1f)), packet => packet.Id, 0f, CreatureMetadataUpdates.Merge);
        throttled.SendThrottled(new EntityMetadataUpdate(id, new CreatureInfectionMetadata(CreatureMetadata.UNDECIDED).WithLeash(leash)), packet => packet.Id, 0f,
                                CreatureMetadataUpdates.Merge);
        throttled.SendThrottled(new EntityMetadataUpdate(id, new CreatureInfectionMetadata(0.5f)), packet => packet.Id, 0f, CreatureMetadataUpdates.Merge);
        throttled.Update();

        EntityMetadataUpdate last = (EntityMetadataUpdate)packetSender.ReceivedCalls().Last().GetArguments()[0];
        CreatureMetadata sent = (CreatureMetadata)last.NewValue;
        Assert.AreEqual(0.5f, sent.InfectedAmount);
        Assert.IsTrue(sent.HasLeash);
        Assert.AreEqual(leash, sent.LeashPosition);
    }
}
