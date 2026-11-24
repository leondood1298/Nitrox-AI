using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class CreatureMetadataAuthorityTest
{
    private static readonly NitroxVector3 leashPosition = new(100f, -250f, 40f);

    [TestMethod]
    public void CuredReaperKeepsItsLeashPosition()
    {
        StayAtLeashPositionMetadata stored = new(leashPosition, 1f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(0f));

        StayAtLeashPositionMetadata leash = accepted.Should().BeOfType<StayAtLeashPositionMetadata>().Subject;
        leash.LeashPosition.Should().Be(leashPosition);
        leash.InfectedAmount.Should().Be(0f);
    }

    [TestMethod]
    public void InfectedSeaTreaderKeepsItsPath()
    {
        SeaTreaderMetadata stored = new(true, 1234f, leashPosition, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(1f));

        SeaTreaderMetadata seaTreader = accepted.Should().BeOfType<SeaTreaderMetadata>().Subject;
        seaTreader.ReverseDirection.Should().BeTrue();
        seaTreader.GrazingEndTime.Should().Be(1234f);
        seaTreader.LeashPosition.Should().Be(leashPosition);
        seaTreader.InfectedAmount.Should().Be(1f);
    }

    [TestMethod]
    public void ContainedFishKeepsItsGrowthWhenCured()
    {
        WaterParkCreatureMetadata stored = new(0.5f, 300d, 200f, true, 1f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(0.1f));

        WaterParkCreatureMetadata fish = accepted.Should().BeOfType<WaterParkCreatureMetadata>().Subject;
        fish.Age.Should().Be(0.5f);
        fish.MatureTime.Should().Be(300d);
        fish.TimeNextBreed.Should().Be(200f);
        fish.BornInside.Should().BeTrue();
        fish.InfectedAmount.Should().Be(0.1f);
    }

    [TestMethod]
    public void BuriedSandSharkStaysBuriedWhenItsInfectionChanges()
    {
        SandSharkMetadata stored = new(true, leashPosition, NitroxQuaternion.Identity, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(1f));

        SandSharkMetadata sandShark = accepted.Should().BeOfType<SandSharkMetadata>().Subject;
        sandShark.Buried.Should().BeTrue();
        sandShark.BurryPosition.Should().Be(leashPosition);
        sandShark.InfectedAmount.Should().Be(1f);
    }

    [TestMethod]
    public void SandSharkGoingUnderKeepsTheDecidedInfection()
    {
        SandSharkMetadata stored = new(false, NitroxVector3.Zero, NitroxQuaternion.Identity, 1f);
        SandSharkMetadata buried = new(true, leashPosition, NitroxQuaternion.Identity, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, buried);

        accepted.Should().BeSameAs(buried);
        accepted!.InfectedAmount.Should().Be(1f);
    }

    [TestMethod]
    public void LavaLarvaAttachingKeepsItsInfectionLeashAndSize()
    {
        StayAtLeashPositionMetadata stored = new(leashPosition, 1f);
        stored.WithSize(0.7f);
        NitroxId prawnId = new();
        LavaLarvaMetadata attaching = new(true, prawnId, 3, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, attaching);

        LavaLarvaMetadata larva = accepted.Should().BeOfType<LavaLarvaMetadata>().Subject;
        larva.Attached.Should().BeTrue();
        larva.VehicleId.Should().Be(prawnId);
        larva.AttachPoint.Should().Be(3);
        larva.InfectedAmount.Should().Be(1f);
        larva.LeashPosition.Should().Be(leashPosition);
        larva.Size.Should().Be(0.7f);
    }

    [TestMethod]
    public void AttachedLavaLarvaStaysAttachedWhenItsInfectionChanges()
    {
        NitroxId seamothId = new();
        LavaLarvaMetadata stored = new(true, seamothId, 1, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(0f));

        LavaLarvaMetadata larva = accepted.Should().BeOfType<LavaLarvaMetadata>().Subject;
        larva.Attached.Should().BeTrue();
        larva.VehicleId.Should().Be(seamothId);
        larva.InfectedAmount.Should().Be(0f);
    }

    [TestMethod]
    public void StalkerKeepsItsFriendWhenCured()
    {
        NitroxId friendId = new();
        StalkerMetadata stored = new(Optional.Of(friendId), 1500d, 1f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(0f));

        StalkerMetadata stalker = accepted.Should().BeOfType<StalkerMetadata>().Subject;
        stalker.FriendId.Value.Should().Be(friendId);
        stalker.FriendshipEndTime.Should().Be(1500d);
        stalker.InfectedAmount.Should().Be(0f);
    }

    [TestMethod]
    public void NewFriendshipKeepsTheDecidedInfection()
    {
        StalkerMetadata friendship = new(Optional.Of(new NitroxId()), 900d, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(new CreatureInfectionMetadata(0.6f), friendship);

        accepted.Should().BeSameAs(friendship);
        accepted!.InfectedAmount.Should().Be(0.6f);
    }

    [TestMethod]
    public void InfectionOfACreatureWithNothingStoredIsStoredAsItIs()
    {
        CreatureInfectionMetadata requested = new(1f);

        CreatureMetadataAuthority.Merge(null, requested).Should().BeSameAs(requested);
    }

    [TestMethod]
    public void InfectionCannotReplaceACorpsesDecay()
    {
        CreatureMetadataAuthority.Merge(new EatableMetadata(50f), new CreatureInfectionMetadata(1f)).Should().BeNull();
    }

    [TestMethod]
    public void PathUpdateKeepsTheDecidedInfection()
    {
        SeaTreaderMetadata stored = new(false, 0f, leashPosition, 1f);
        SeaTreaderMetadata newPath = new(true, 500f, leashPosition, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, newPath);

        accepted.Should().BeSameAs(newPath);
        accepted!.InfectedAmount.Should().Be(1f);
        ((SeaTreaderMetadata)accepted).ReverseDirection.Should().BeTrue();
    }

    [TestMethod]
    public void NewCreatureMetadataKeepsItsOwnInfectionWhenNothingIsDecided()
    {
        WaterParkCreatureMetadata requested = new(0f, 300d, 200f, false, 0.4f);

        CreatureMetadataAuthority.Merge(new CreatureInfectionMetadata(CreatureMetadata.UNDECIDED), requested)!.InfectedAmount.Should().Be(0.4f);
    }

    [TestMethod]
    public void HeroPeeperKeepsItsDecidedInfectionWhenItsEnzymeIsSentWithoutOne()
    {
        CreatureInfectionMetadata stored = new(0.5f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(CreatureMetadata.UNDECIDED, 1060f));

        accepted!.InfectedAmount.Should().Be(0.5f);
        accepted.EnzymeEndTime.Should().Be(1060f);
    }

    [TestMethod]
    public void InfectionUpdateDoesNotTakeAwayAHeroPeepersEnzyme()
    {
        CreatureInfectionMetadata stored = new(1f, 1060f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(0f));

        accepted!.InfectedAmount.Should().Be(0f);
        accepted.EnzymeEndTime.Should().Be(1060f);
    }

    [TestMethod]
    public void NewerEnzymeOfAHeroPeeperIsKept()
    {
        CreatureMetadataAuthority.Merge(new CreatureInfectionMetadata(0f, 1060f), new CreatureInfectionMetadata(0f, 2000f))!.EnzymeEndTime.Should().Be(2000f);
    }

    [TestMethod]
    public void ContainedHeroPeeperKeepsItsEnzymeWhenItGrows()
    {
        WaterParkCreatureMetadata stored = new(0.5f, 300d, 200f, true, 0f, CreatureMetadata.LASTING_ENZYME);
        WaterParkCreatureMetadata grown = new(0.6f, 300d, 200f, true, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, grown);

        accepted.Should().BeSameAs(grown);
        accepted!.EnzymeEndTime.Should().Be(CreatureMetadata.LASTING_ENZYME);
        accepted.InfectedAmount.Should().Be(0f);
    }

    [TestMethod]
    public void OutsideBabysDropsOnlyAddUp()
    {
        SeaEmperorBabyMetadata stored = new(2.02f, 3, 5, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new SeaEmperorBabyMetadata(2.02f, 1, 5, CreatureMetadata.UNDECIDED));

        accepted.Should().BeOfType<SeaEmperorBabyMetadata>().Which.DropCount.Should().Be(3);
    }

    [TestMethod]
    public void OutsideBabysNextDropIsStored()
    {
        SeaEmperorBabyMetadata requested = new(2.02f, 4, 5, CreatureMetadata.UNDECIDED);

        CreatureMetadataAuthority.Merge(new SeaEmperorBabyMetadata(2.02f, 3, 5, CreatureMetadata.UNDECIDED), requested).Should().BeSameAs(requested);
    }

    [TestMethod]
    public void OutsideBabyKeepsItsCureWhenItDrops()
    {
        SeaEmperorBabyMetadata stored = new(2.02f, 3, 5, 0f, 1060f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new SeaEmperorBabyMetadata(2.02f, 2, 5, CreatureMetadata.UNDECIDED));

        SeaEmperorBabyMetadata baby = accepted.Should().BeOfType<SeaEmperorBabyMetadata>().Subject;
        baby.DropCount.Should().Be(3);
        baby.Scale.Should().Be(2.02f);
        baby.InfectedAmount.Should().Be(0f);
        baby.EnzymeEndTime.Should().Be(1060f);
    }

    [TestMethod]
    public void LeashAndSizeSentByTheSimulatingPlayerAreStored()
    {
        StayAtLeashPositionMetadata stored = new(leashPosition, 0.2f);
        NitroxVector3 newLeash = new(5f, -30f, 7f);
        CreatureMetadata update = new CreatureInfectionMetadata(CreatureMetadata.UNDECIDED).WithLeash(newLeash).WithSize(0.7f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, (CreatureInfectionMetadata)update);

        accepted.Should().BeSameAs(stored);
        accepted!.LeashPosition.Should().Be(newLeash);
        accepted.HasSize.Should().BeTrue();
        accepted.Size.Should().Be(0.7f);
        accepted.InfectedAmount.Should().Be(0.2f);
    }

    [TestMethod]
    public void InfectionUpdateKeepsTheLeashAndSize()
    {
        CreatureMetadata stored = new StayAtLeashPositionMetadata(leashPosition, 0.2f).WithSize(0.3f);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(1f));

        accepted!.LeashPosition.Should().Be(leashPosition);
        accepted.Size.Should().Be(0.3f);
    }

    [TestMethod]
    public void StalkerFriendshipKeepsTheSeededLeash()
    {
        StalkerMetadata friendship = new(Optional.Of(new NitroxId()), 900d, CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(new StayAtLeashPositionMetadata(leashPosition, CreatureMetadata.UNDECIDED), friendship);

        accepted.Should().BeSameAs(friendship);
        accepted!.HasLeash.Should().BeTrue();
        accepted.LeashPosition.Should().Be(leashPosition);
    }

    [TestMethod]
    public void CuddlefishKeepsFollowingItsLeaderWhenItsInfectionChanges()
    {
        NitroxId leaderId = new();
        CuteFishMetadata stored = new(true, Optional.Of(leaderId), CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, new CreatureInfectionMetadata(0f));

        CuteFishMetadata cuteFish = accepted.Should().BeOfType<CuteFishMetadata>().Subject;
        cuteFish.FollowingPlayer.Should().BeTrue();
        cuteFish.LeaderId.Value.Should().Be(leaderId);
    }

    [TestMethod]
    public void CommandToACuddlefishKeepsItsLeashAndInfection()
    {
        CreatureMetadata stored = new StayAtLeashPositionMetadata(leashPosition, 0.3f);
        CuteFishMetadata command = new(false, Optional.Of(new NitroxId()), CreatureMetadata.UNDECIDED);

        CreatureMetadata? accepted = CreatureMetadataAuthority.Merge(stored, command);

        accepted.Should().BeSameAs(command);
        accepted!.InfectedAmount.Should().Be(0.3f);
        accepted.LeashPosition.Should().Be(leashPosition);
    }

    [TestMethod]
    public void SimulatingPlayerDecidesInfection()
    {
        Player sender = CreatePlayer(1, 1);

        CreatureMetadataAuthority.IsFromSimulatingPlayer(sender, sender).Should().BeTrue();
    }

    [TestMethod]
    public void InfectionFromAPlayerWhoDoesNotSimulateTheCreatureIsRefused()
    {
        CreatureMetadataAuthority.IsFromSimulatingPlayer(CreatePlayer(2, 2), CreatePlayer(1, 1)).Should().BeFalse();
        CreatureMetadataAuthority.IsFromSimulatingPlayer(null, CreatePlayer(1, 1)).Should().BeFalse();
    }

    [TestMethod]
    public void InfectionFromThePreviousSessionOfTheSimulatingPlayerIsRefused()
    {
        // Player == compares PeerId: a reconnected player must not pass for its previous session
        CreatureMetadataAuthority.IsFromSimulatingPlayer(CreatePlayer(1, 1), CreatePlayer(1, 2)).Should().BeFalse();
    }

    private static Player CreatePlayer(ushort peerId, ushort sessionId)
    {
        return new Player(peerId, sessionId, $"Player{sessionId}", false, null, NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxId(), Optional.Empty, Perms.PLAYER,
                          new PlayerStatsData(100, 100, 100, 100, 100, 0), SubnauticaGameMode.SURVIVAL, [], [], new Dictionary<string, NitroxId>(), new Dictionary<string, float>(),
                          new Dictionary<string, PingInstancePreference>(), [], false, true);
    }
}
