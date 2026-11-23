using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Helpers;
using NSubstitute;
using UnityEngine;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class MapRoomScansTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private readonly SimulationOwnership simulationOwnership;
    private readonly MapRoomScans scans;
    private readonly FakeRoom room;
    private float now = 100f;
    private bool worldLoaded = true;

    public MapRoomScansTest()
    {
        packetSender.Send(Arg.Any<MapRoomScanSubscription>()).Returns(true);
        simulationOwnership = new SimulationOwnership(Substitute.For<IMultiplayerSession>(), packetSender);
        scans = new MapRoomScans(packetSender, new ThrottledPacketSender(packetSender), simulationOwnership, () => now, () => worldLoaded);
        room = new FakeRoom(new NitroxId(), scans);
    }

    [TestMethod]
    public void StateFromTheServerStartsTheScanAndIsShownWithoutTellingTheServer()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 3, 2));

        Assert.AreEqual(TechType.Quartz, room.Target);
        Assert.AreEqual(3, room.NodesScanned);
        Assert.AreEqual(1, room.TargetsShown);
        Assert.AreEqual(2, scans.GetGeneration(room.Id));
        Assert.AreEqual(0, Sent<Packet>().Count);
    }

    [TestMethod]
    public void FirstStateIsAppliedEvenWhenTheTargetAlreadyMatches()
    {
        // A room that was just built scans for nothing, and the game still has to be told to show that.
        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0));
        Assert.AreEqual(1, room.ScansStarted);

        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0));

        Assert.AreEqual(1, room.ScansStarted, "After that, the game is only told when the target changes.");
    }

    [TestMethod]
    public void ProgressFromTheServerIsShownWithoutStartingTheScanAgain()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 2, 4));

        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 4));

        Assert.AreEqual(5, room.NodesScanned);
        Assert.AreEqual(1, room.ScansStarted);
    }

    [TestMethod]
    public void StateOfAnOlderTargetIsIgnored()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(titanium, 1, 3));

        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 9, 2));

        Assert.AreEqual(TechType.Titanium, room.Target);
        Assert.AreEqual(1, room.NodesScanned);
        Assert.AreEqual(3, scans.GetGeneration(room.Id));
    }

    [TestMethod]
    public void ChosenTargetIsSentWithTheGenerationItWasChosenAgainst()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0, 6));
        room.StartScanning(TechType.Quartz);

        scans.TargetChosen(room);

        MapRoomMetadata sent = (MapRoomMetadata)Sent<EntityMetadataUpdate>().Single().NewValue;
        Assert.AreEqual(quartz, sent.TypeToScan);
        Assert.AreEqual(0, sent.NumNodesScanned);
        Assert.AreEqual(6, sent.Generation);
        Assert.AreEqual(6, scans.GetGeneration(room.Id), "Only the server counts the generations.");
    }

    [TestMethod]
    public void StoppingAScanIsSentLikeAnyOtherTargetAPlayerChooses()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 6));
        room.StartScanning(TechType.None);

        scans.TargetChosen(room);

        MapRoomMetadata sent = (MapRoomMetadata)Sent<EntityMetadataUpdate>().Single().NewValue;
        Assert.AreEqual(NitroxTechType.None, sent.TypeToScan);
        Assert.AreEqual(6, sent.Generation);
    }

    [TestMethod]
    public void TargetThatLostAgainstANewerOneIsReplacedByTheServersAndShown()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0, 3));
        room.StartScanning(TechType.Quartz);
        scans.TargetChosen(room);
        int shownBefore = room.TargetsShown;

        // Another player's target reached the server first. This client is sent that target like everyone, and then
        // the same state again as the answer to its own request, which the server refused.
        scans.ApplyMetadata(room, new MapRoomMetadata(titanium, 0, 4));
        scans.ApplyMetadata(room, new MapRoomMetadata(titanium, 0, 4));

        Assert.AreEqual(TechType.Titanium, room.Target);
        Assert.AreEqual(shownBefore + 1, room.TargetsShown, "The screen showed this player's own choice until now.");

        room.StartScanning(TechType.Quartz);
        scans.TargetChosen(room);
        Assert.AreEqual(4, ((MapRoomMetadata)Sent<EntityMetadataUpdate>().Last().NewValue).Generation, "With the generation it was given, its next choice can be accepted.");
    }

    [TestMethod]
    public void RefusedTargetIsReplacedByTheServersEvenWhenTheGenerationIsTheOneThisClientKnows()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(titanium, 2, 3));
        room.StartScanning(TechType.Quartz);
        scans.TargetChosen(room);

        scans.ApplyMetadata(room, new MapRoomMetadata(titanium, 2, 3));

        Assert.AreEqual(TechType.Titanium, room.Target);
        Assert.AreEqual(2, room.NodesScanned);
    }

    [TestMethod]
    public void SimulatingPlayerKeepsAndReportsWhatItRevealedWhileItsTargetWasOnItsWay()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0));
        room.Loaded.AddRange([Info("first", 1f), Info("second", 2f)]);
        room.StartScanning(TechType.Quartz);
        scans.TargetChosen(room);
        // The game goes on scanning during the round trip.
        room.NodesScanned = 2;

        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 1));

        Assert.AreEqual(2, room.NodesScanned, "The server's answer must not hide what the scan has already revealed.");
        MapRoomMetadata progress = (MapRoomMetadata)Sent<EntityMetadataUpdate>().Last().NewValue;
        Assert.AreEqual(2, progress.NumNodesScanned);
        Assert.AreEqual(1, progress.Generation, "The progress is sent again for the generation the server gave the target.");
    }

    [TestMethod]
    public void PlayerWhoDoesNotSimulateShowsTheServersProgressForItsOwnTarget()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0));
        room.StartScanning(TechType.Quartz);
        scans.TargetChosen(room);
        room.NodesScanned = 2;

        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 1));

        Assert.AreEqual(0, room.NodesScanned);
        Assert.AreEqual(1, Sent<EntityMetadataUpdate>().Count, "Only the chosen target was sent.");
    }

    [TestMethod]
    public void RevealedResultsAreOnlyReportedByTheSimulatingPlayerForATargetTheServerHasConfirmed()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 4));
        room.NodesScanned = 1;
        scans.NodeScanned(room);
        Assert.AreEqual(0, Sent<EntityMetadataUpdate>().Count, "Every client's game reveals results. Only one of them counts.");

        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.NodeScanned(room);
        MapRoomMetadata progress = (MapRoomMetadata)Sent<EntityMetadataUpdate>().Single().NewValue;
        Assert.AreEqual(1, progress.NumNodesScanned);
        Assert.AreEqual(4, progress.Generation);

        room.StartScanning(TechType.Titanium);
        room.NodesScanned = 5;
        scans.NodeScanned(room);
        Assert.AreEqual(1, Sent<EntityMetadataUpdate>().Count, "Progress of a target the server does not know yet would be refused.");
    }

    [TestMethod]
    public void NumberOfRevealedResultsTheServerAlreadyHasIsNotSentAgain()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 6, 4));

        // The game lowers its number when results are gone, and counts up again from there.
        foreach (int revealed in new[] { 4, 5, 6 })
        {
            room.NodesScanned = revealed;
            scans.NodeScanned(room);
        }
        Assert.AreEqual(0, Sent<EntityMetadataUpdate>().Count, "The server would refuse these and answer each with the 6 it holds.");

        room.NodesScanned = 7;
        scans.NodeScanned(room);
        scans.NodeScanned(room);

        Assert.AreEqual(7, ((MapRoomMetadata)Sent<EntityMetadataUpdate>().Single().NewValue).NumNodesScanned);
    }

    [TestMethod]
    public void PlayerWhoStartsSimulatingARoomReportsWhatItsOwnGameFinds()
    {
        // After a server start the server holds no results and no types. Whoever is given the room has to bring them.
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 4, 7));
        scans.ApplySnapshot(room, new MapRoomScanSnapshot(room.Id, 7,
            Enumerable.Range(0, 5).Select(index => new MapRoomScanResultRecord($"from-the-server-{index}", quartz, new NitroxVector3(5f, 0f, 0f))).ToList(), [titanium]));
        room.Loaded.AddRange([Info("near", 10f), Info("far", 400f)]);
        room.LoadedTypes.Add(TechType.Quartz);
        scans.Update(room);
        Assert.AreEqual(0, Sent<MapRoomScanReport>().Count + Sent<MapRoomScanTypesReport>().Count, "A player who does not simulate the room reports nothing.");

        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.Update(room);
        scans.Update(room);

        MapRoomScanReport report = Sent<MapRoomScanReport>().Single();
        Assert.AreEqual(room.Id, report.MapRoomId);
        Assert.AreEqual(7, report.Generation);
        Assert.AreEqual(new NitroxVector3(1f, 2f, 3f), report.ScanOrigin);
        Assert.AreEqual(300f, report.ScanRange);
        CollectionAssert.AreEqual(new[] { "near" }, report.Results.Select(result => result.ResourceId).ToArray(), "Only what this client's game has in range.");
        MapRoomScanTypesReport types = Sent<MapRoomScanTypesReport>().Single();
        CollectionAssert.AreEqual(new[] { quartz }, types.TechTypes);
        CollectionAssert.AreEqual(new[] { quartz, titanium }, types.DetectableTechTypes);
        Assert.AreEqual(4, room.NodesScanned, "The progress is the room's, whoever simulates it.");
    }

    [TestMethod]
    public void SimulatingPlayerReportsAgainForEveryNewTarget()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 1));
        room.Loaded.AddRange([Info("quartz", 10f), Info("titanium", 20f, TechType.Titanium)]);
        scans.Update(room);
        Assert.AreEqual(1, Sent<MapRoomScanReport>().Count);

        // Another player chose this target.
        scans.ApplyMetadata(room, new MapRoomMetadata(titanium, 0, 2));
        scans.Update(room);

        MapRoomScanReport report = Sent<MapRoomScanReport>().Last();
        Assert.AreEqual(2, Sent<MapRoomScanReport>().Count);
        Assert.AreEqual(2, report.Generation);
        CollectionAssert.AreEqual(new[] { "titanium" }, report.Results.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void ReportForATargetThisPlayerChoseWaitsForTheServersGeneration()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0, 3));
        scans.Update(room);
        room.Loaded.Add(Info("quartz", 10f));
        room.StartScanning(TechType.Quartz);
        scans.TargetChosen(room);

        scans.Update(room);
        Assert.AreEqual(1, Sent<MapRoomScanReport>().Count, "A report naming generation 3 would be dropped as one for the previous target.");
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 4));
        scans.Update(room);

        MapRoomScanReport report = Sent<MapRoomScanReport>().Last();
        Assert.AreEqual(4, report.Generation);
        Assert.AreEqual("quartz", report.Results.Single().ResourceId);
    }

    [TestMethod]
    public void PlayerWhoStopsSimulatingAsksTheServerForItsList()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.Update(room);

        simulationOwnership.StopSimulatingEntity(room.Id);
        scans.Update(room);
        scans.Update(room);

        Assert.AreEqual(2, Sent<MapRoomScanSubscription>().Count);
        Assert.IsTrue(Sent<MapRoomScanSubscription>().All(subscription => subscription.Subscribed && subscription.MapRoomId == room.Id));
    }

    [TestMethod]
    public void RoomIsSubscribedToOnceForAsLongAsItExists()
    {
        // The room's screen being switched off and on again plays no part in it.
        scans.Update(room);
        scans.Update(room);
        scans.Update(room);
        MapRoomScanSubscription subscription = Sent<MapRoomScanSubscription>().Single();
        Assert.AreEqual(room.Id, subscription.MapRoomId);
        Assert.IsTrue(subscription.Subscribed);

        scans.RoomDestroyed(room);
        scans.RoomDestroyed(room);

        Assert.AreEqual(2, Sent<MapRoomScanSubscription>().Count);
        Assert.IsFalse(Sent<MapRoomScanSubscription>().Last().Subscribed);
    }

    [TestMethod]
    public void RoomBuiltAgainWithTheSameIdIsSubscribedToAgainAndStartsFromNothing()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 2, 5));
        scans.Update(room);
        FakeRoom rebuilt = new(room.Id, scans);

        scans.Update(rebuilt);
        // The old room's object can be destroyed after the new one is there.
        scans.RoomDestroyed(room);

        Assert.AreEqual(2, Sent<MapRoomScanSubscription>().Count);
        Assert.IsTrue(Sent<MapRoomScanSubscription>().All(subscription => subscription.Subscribed), "The new room's subscription must not be ended with the old room.");
        Assert.AreEqual(0, scans.GetGeneration(room.Id), "The server counts the new room's targets from zero.");
        scans.ApplySnapshot(rebuilt, new MapRoomScanSnapshot(room.Id, 0, [new MapRoomScanResultRecord("found", quartz, NitroxVector3.Zero)], []));
        Assert.AreEqual(1, rebuilt.Nodes.Count);
    }

    [TestMethod]
    public void SubscriptionThatCouldNotBeSentIsSentLater()
    {
        // Packets are held back while a base is built anew, which is when a room starts existing.
        packetSender.Send(Arg.Any<MapRoomScanSubscription>()).Returns(false, true);

        scans.Update(room);
        scans.Update(room);
        scans.Update(room);

        Assert.AreEqual(2, Sent<MapRoomScanSubscription>().Count);
    }

    [TestMethod]
    public void SimulatingPlayerWhoseGameSeesTheRoomLosePowerStopsTheScanForEveryone()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 6));
        scans.Update(room);
        int shownBefore = room.TargetsShown;

        room.Powered = false;
        scans.Update(room);
        scans.Update(room);

        Assert.AreEqual(TechType.None, room.Target);
        Assert.AreEqual(shownBefore + 1, room.TargetsShown);
        MapRoomMetadata sent = (MapRoomMetadata)Sent<EntityMetadataUpdate>().Single().NewValue;
        Assert.AreEqual(NitroxTechType.None, sent.TypeToScan);
        Assert.AreEqual(6, sent.Generation, "It is sent like a target a player chose, so the server tells everyone.");

        room.Powered = true;
        scans.Update(room);
        Assert.AreEqual(TechType.None, room.Target, "The game does not take a scan up again when the power returns.");
    }

    [TestMethod]
    public void PlayerWhoDoesNotSimulateStopsNothingWhenItsOwnGameSeesThePowerGo()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 6));
        scans.Update(room);

        room.Powered = false;
        scans.Update(room);

        Assert.AreEqual(TechType.Quartz, room.Target);
        Assert.AreEqual(0, Sent<EntityMetadataUpdate>().Count, "Whether the room has lost its power is for the simulating player's game to say.");
    }

    [TestMethod]
    public void RoomThatIsDestroyedWhileItHasPowerKeepsItsScan()
    {
        // The game switches the room's screen off with a room that is destroyed, as it does when the power goes.
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 6));
        scans.Update(room);

        scans.RoomDestroyed(room);

        Assert.AreEqual(TechType.Quartz, room.Target);
        Assert.AreEqual(0, Sent<EntityMetadataUpdate>().Count, "The room built in its place goes on with the scan the server holds.");
    }

    [TestMethod]
    public void PowerThatIsMissingWhenARoomStartsExistingWasNotLost()
    {
        // A room is spawned with its scan. Its game may connect it to the power of its base a moment later.
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        room.Powered = false;
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 6));

        scans.Update(room);
        room.Powered = true;
        scans.Update(room);

        Assert.AreEqual(TechType.Quartz, room.Target);
        Assert.AreEqual(0, Sent<EntityMetadataUpdate>().Count);
    }

    [TestMethod]
    public void PowerThatGoesWhileTheWorldIsStillLoadingIsNoLoss()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 6));
        scans.Update(room);

        // The power sources of a base are brought in one after the other while this client joins or builds its bases
        // anew. The game itself counts a base as powered while it loads.
        worldLoaded = false;
        room.Powered = false;
        scans.Update(room);
        worldLoaded = true;
        room.Powered = true;
        scans.Update(room);

        Assert.AreEqual(TechType.Quartz, room.Target);
        Assert.AreEqual(0, Sent<EntityMetadataUpdate>().Count);

        room.Powered = false;
        scans.Update(room);
        Assert.AreEqual(TechType.None, room.Target, "A loss after that is a real one.");
    }

    [TestMethod]
    public void RoomThatScansForNothingHasNoScanToStopWhenItLosesPower()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(NitroxTechType.None, 0, 6));
        scans.Update(room);
        int startedBefore = room.ScansStarted;

        room.Powered = false;
        scans.Update(room);

        Assert.AreEqual(startedBefore, room.ScansStarted);
        Assert.AreEqual(0, Sent<EntityMetadataUpdate>().Count);
    }

    [TestMethod]
    public void SnapshotReplacesTheResultsInTheServersOrder()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 5, 2));
        room.Nodes.Add(Info("stale", 0f));

        scans.ApplySnapshot(room, new MapRoomScanSnapshot(room.Id, 2,
        [
            new MapRoomScanResultRecord("first", quartz, new NitroxVector3(1f, 0f, 0f)),
            new MapRoomScanResultRecord("second", quartz, new NitroxVector3(2f, 0f, 0f))
        ], []));

        CollectionAssert.AreEqual(new[] { "first", "second" }, room.Nodes.Select(info => info.uniqueId).ToArray());
        Assert.AreEqual(2, room.NodesScanned, "The scan cannot have revealed more than there is.");
        Assert.IsTrue(room.ResultsShown > 0);
    }

    [TestMethod]
    public void ResultsOfAnotherTargetAreIgnored()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 2));
        room.Nodes.Add(Info("current", 0f));
        MapRoomScanResultRecord late = new("late", titanium, NitroxVector3.Zero);

        scans.ApplySnapshot(room, new MapRoomScanSnapshot(room.Id, 1, [late], []));
        scans.ApplyChange(room, new MapRoomScanResultChanged(room.Id, 1, late, false));
        scans.ApplyChange(room, new MapRoomScanResultChanged(room.Id, 3, late, false));

        CollectionAssert.AreEqual(new[] { "current" }, room.Nodes.Select(info => info.uniqueId).ToArray());
    }

    [TestMethod]
    public void ChangeAddsUpdatesAndRemovesByStableId()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 1));
        room.Nodes.AddRange([Info("resource", 1f), Info("resource", 1f)]);

        scans.ApplyChange(room, new MapRoomScanResultChanged(room.Id, 1, new MapRoomScanResultRecord("resource", quartz, new NitroxVector3(4f, 0f, 0f)), false));
        Assert.AreEqual(4f, room.Nodes.Single().position.x);
        scans.ApplyChange(room, new MapRoomScanResultChanged(room.Id, 1, new MapRoomScanResultRecord("new", quartz, new NitroxVector3(2f, 0f, 0f)), false));
        scans.ApplyChange(room, new MapRoomScanResultChanged(room.Id, 1, new MapRoomScanResultRecord("resource", quartz, NitroxVector3.Zero), true));

        Assert.AreEqual("new", room.Nodes.Single().uniqueId);
    }

    [TestMethod]
    public void LiveDiscoveryReplacesSyntheticStableIdAndTheServersEchoPreservesTheLiveReference()
    {
        ResourceTrackerDatabase.ResourceInfo synthetic = Info("hybrid", 1f);
        ResourceTrackerDatabase.ResourceInfo live = Info("hybrid", 2f);
        List<ResourceTrackerDatabase.ResourceInfo> local = [synthetic, live];

        Assert.IsTrue(MapRoomScans.UpsertLiveDiscoveredResource(local, live));
        Assert.AreSame(live, local.Single());

        MapRoomScans.ApplyChangeToList(local, new MapRoomScanResultChanged(new NitroxId(), 1, new MapRoomScanResultRecord("hybrid", quartz, new NitroxVector3(3f, 0f, 0f)), false));

        Assert.AreSame(live, local.Single(), "The game removes this entry by reference when its cell unloads.");
        Assert.AreEqual(3f, live.position.x);
        Assert.AreEqual(TechType.Quartz, live.techType);
    }

    [TestMethod]
    public void SimulatingPlayerReportsADiscoveredResourceAndEvictsOneOutOfRange()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 3));
        ResourceTrackerDatabase.ResourceInfo near = Info("near", 10f);
        // The game adds a matching resource within 500 m, whatever the room's range is.
        ResourceTrackerDatabase.ResourceInfo far = Info("far", 400f);
        room.Nodes.AddRange([Info("near", 9f), near, Info("far", 1f), far]);

        scans.ResourceDiscovered(room, near);
        scans.ResourceDiscovered(room, far);

        Assert.AreSame(near, room.Nodes.Single(), "The entry the game added is the one that stays.");
        List<MapRoomScanResultReport> reports = Sent<MapRoomScanResultReport>();
        Assert.AreEqual(2, reports.Count);
        Assert.AreEqual("near", reports[0].Result.ResourceId);
        Assert.IsFalse(reports[0].Removed || reports[0].IsRangeExit);
        Assert.AreEqual(3, reports[0].Generation);
        Assert.AreEqual(new NitroxVector3(1f, 2f, 3f), reports[0].ScanOrigin);
        Assert.AreEqual(300f, reports[0].ScanRange);
        Assert.AreEqual("far", reports[1].Result.ResourceId);
        Assert.IsTrue(reports[1].Removed && reports[1].IsRangeExit);
    }

    [TestMethod]
    public void SimulatingPlayerReportsAnUnloadedResourceAsGoneButNotAsOutOfRange()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 3));
        ResourceTrackerDatabase.ResourceInfo unloaded = Info("unloaded", 10f);
        // The game removed its own entry. One the server had added under the same id is still there.
        room.Nodes.AddRange([Info("unloaded", 10f), Info("other", 20f)]);

        scans.ResourceRemoved(room, unloaded);
        scans.ResourceRemoved(room, Info("titanium", 10f, TechType.Titanium));

        Assert.AreEqual("other", room.Nodes.Single().uniqueId);
        MapRoomScanResultReport report = Sent<MapRoomScanResultReport>().Single();
        Assert.AreEqual("unloaded", report.Result.ResourceId);
        Assert.IsTrue(report.Removed);
        Assert.IsFalse(report.IsRangeExit, "Only the server knows whether a resource this client unloaded still exists.");
    }

    [TestMethod]
    public void PlayerWhoDoesNotSimulateReportsNoResults()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 3));
        ResourceTrackerDatabase.ResourceInfo info = Info("resource", 10f);
        room.Nodes.Add(info);

        scans.ResourceDiscovered(room, info);
        scans.ResourceRemoved(room, info);
        scans.ResourceMoved(room, info);
        scans.ResultsObtained(room);
        scans.Update(room);

        Assert.IsTrue(Sent<Packet>().All(packet => packet is MapRoomScanSubscription));
        Assert.AreSame(info, room.Nodes.Single());
    }

    [TestMethod]
    public void PlayerWhoDoesNotSimulateNeverFillsTheResultsFromItsOwnGame()
    {
        room.Loaded.Add(Info("only-this-client-has-it-loaded", 10f));

        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 3));
        Assert.AreEqual(0, room.Nodes.Count, "The game fills the list when a scan starts.");
        Assert.IsFalse(scans.ScansLocally(room), "The game adds to the list when a resource is loaded.");

        scans.ApplySnapshot(room, new MapRoomScanSnapshot(room.Id, 3, [new MapRoomScanResultRecord("from-the-server", quartz, new NitroxVector3(5f, 0f, 0f))], []));
        // The game fills the list again when a range upgrade is put in or taken out.
        room.ScanRange = 350f;
        room.ObtainNodes();

        Assert.AreEqual("from-the-server", room.Nodes.Single().uniqueId);
        Assert.AreEqual(0, Sent<Packet>().Count);
    }

    [TestMethod]
    public void PlayerWhoDoesNotSimulateDropsTheResultsOfThePreviousTarget()
    {
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 1, 3));
        scans.ApplySnapshot(room, new MapRoomScanSnapshot(room.Id, 3, [new MapRoomScanResultRecord("quartz", quartz, new NitroxVector3(5f, 0f, 0f))], []));

        // This player picks another target; the server's answer comes later.
        room.StartScanning(TechType.Titanium);
        Assert.AreEqual(0, room.Nodes.Count);

        room.Nodes.Add(Info("left-over", 1f, TechType.Titanium));
        scans.ApplyMetadata(room, new MapRoomMetadata(titanium, 0, 4));
        Assert.AreEqual(0, room.Nodes.Count, "The results of a new generation only come from the server.");
    }

    [TestMethod]
    public void SimulatingPlayerScansWithItsOwnGameAndReportsAgainWhenTheGameRebuiltTheList()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        room.Loaded.AddRange([Info("near", 10f), Info("further", 320f)]);
        scans.ApplyMetadata(room, new MapRoomMetadata(quartz, 0, 3));
        scans.Update(room);
        Assert.IsTrue(scans.ScansLocally(room));
        CollectionAssert.AreEqual(new[] { "near" }, Sent<MapRoomScanReport>().Single().Results.Select(result => result.ResourceId).ToArray());

        // A range upgrade was put in: the game fills the list again, without what the server had added to it.
        room.ScanRange = 350f;
        room.ObtainNodes();
        scans.Update(room);
        scans.Update(room);

        MapRoomScanReport report = Sent<MapRoomScanReport>().Last();
        Assert.AreEqual(2, Sent<MapRoomScanReport>().Count);
        Assert.AreEqual(350f, report.ScanRange);
        CollectionAssert.AreEqual(new[] { "near", "further" }, report.Results.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void OutOfRangeEvictionRemovesAllEntriesWithTheStableId()
    {
        ResourceTrackerDatabase.ResourceInfo discovered = Info("outside", 400f);
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("outside", 1f), discovered, Info("inside", 2f)];

        Assert.IsTrue(MapRoomScans.EvictDiscoveredResourceFromList(local, discovered));

        Assert.AreEqual("inside", local.Single().uniqueId);
    }

    [TestMethod]
    public void EvictionOfAnEntryWithoutIdFallsBackToItsReference()
    {
        ResourceTrackerDatabase.ResourceInfo invalid = Info(string.Empty, 1f);
        ResourceTrackerDatabase.ResourceInfo otherInvalid = Info(string.Empty, 2f);
        List<ResourceTrackerDatabase.ResourceInfo> local = [otherInvalid, invalid];

        Assert.IsTrue(MapRoomScans.EvictDiscoveredResourceFromList(local, invalid));

        Assert.AreSame(otherInvalid, local.Single());
    }

    [TestMethod]
    public void PickedUpResourceIsTakenOutOfTheResultsAndTheHudCacheById()
    {
        room.Nodes.AddRange([Info("collected", 1f), Info("collected", 1f), Info("remaining", 2f)]);
        room.NodesScanned = 3;
        HashSet<ResourceTrackerDatabase.ResourceInfo> hud = [Info("collected", 1f), Info("remaining", 2f)];

        MapRoomScans.RemoveResource(room, "collected", TechType.Quartz, new Vector3(50f, 0f, 0f));
        MapRoomScans.RemoveResource(room, "missing", TechType.Quartz, new Vector3(50f, 0f, 0f));
        Assert.IsTrue(MapRoomScans.RemoveFromSet(hud, "collected"));
        Assert.IsFalse(MapRoomScans.RemoveFromSet(hud, "missing"));

        Assert.AreEqual("remaining", room.Nodes.Single().uniqueId);
        Assert.AreEqual(1, room.NodesScanned);
        Assert.AreEqual("remaining", hud.Single().uniqueId);
    }

    [TestMethod]
    public void PickedUpResourceIsFoundByTypeAndPositionWhenItsIdChanged()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("legacy-id", 1f), Info("nearby-resource", 1.2f), Info("same-id-elsewhere", 3f)];

        Assert.IsTrue(MapRoomScans.RemoveFromList(local, "same-id-elsewhere", TechType.Quartz, Info("picked-up", 1f).position));

        Assert.AreEqual("nearby-resource", local.Single().uniqueId);
    }

    [TestMethod]
    public void RepeatedResourceEventsWithTheSameTypesSendOneReport()
    {
        // Streaming in an area registers hundreds of resources of types the room already lists.
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        room.ShownTypes = [TechType.Quartz, TechType.Titanium];

        for (int resource = 0; resource < 300; resource++)
        {
            now += 0.01f;
            scans.ReportTypes(room);
        }

        MapRoomScanTypesReport report = Sent<MapRoomScanTypesReport>().Single();
        CollectionAssert.AreEquivalent(new[] { quartz, titanium }, report.TechTypes);
        Assert.AreEqual(new NitroxVector3(1f, 2f, 3f), report.ScanOrigin);
        Assert.AreEqual(300f, report.ScanRange);
    }

    [TestMethod]
    public void NewTypeRemovedTypeAndRangeChangeAreEachReportedAtOnce()
    {
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        room.ShownTypes = [TechType.Quartz];
        scans.ReportTypes(room);

        room.ShownTypes!.Add(TechType.Titanium);
        scans.ReportTypes(room);
        scans.ReportTypes(room);
        Assert.AreEqual(2, Sent<MapRoomScanTypesReport>().Count, "A newly discovered type must be reported.");

        room.ShownTypes.Remove(TechType.Quartz);
        scans.ReportTypes(room);
        Assert.AreEqual(3, Sent<MapRoomScanTypesReport>().Count, "A type with no resources left must be reported.");

        room.ScanRange = 350f;
        scans.ReportTypes(room);
        scans.ReportTypes(room);
        Assert.AreEqual(4, Sent<MapRoomScanTypesReport>().Count, "A range upgrade changes which resources are in range.");
    }

    [TestMethod]
    public void UnchangedTypesAreReportedAgainAfterAWhileAndOnlyByTheSimulatingPlayer()
    {
        FakeRoom other = new(new NitroxId(), scans) { ShownTypes = [TechType.Quartz] };
        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        room.ShownTypes = [TechType.Quartz];
        scans.ReportTypes(room);
        scans.ReportTypes(other);
        Assert.AreEqual(room.Id, Sent<MapRoomScanTypesReport>().Single().MapRoomId);

        now += MapRoomScans.UNCHANGED_TYPES_REFRESH_SECONDS - 0.1f;
        scans.ReportTypes(room);
        Assert.AreEqual(1, Sent<MapRoomScanTypesReport>().Count);
        now += 0.2f;
        scans.ReportTypes(room);

        Assert.AreEqual(2, Sent<MapRoomScanTypesReport>().Count, "What the server adds changes with the world, not with this client's game.");
    }

    [TestMethod]
    public void PlayerWhoDoesNotSimulateListsTheServersTypesOnceItHasThem()
    {
        room.ShownTypes = [TechType.Quartz];
        Assert.IsTrue(scans.ObtainsTypesLocally(room), "Until the server has sent its list, every client lists what its own game finds.");

        scans.ApplyTypes(room, new MapRoomScanTypesChanged(room.Id, [titanium]));
        CollectionAssert.AreEquivalent(new[] { TechType.Titanium }, room.ShownTypes.ToArray());

        // The game's own handler changed the list on the screen.
        room.ShownTypes.Add(TechType.Quartz);
        Assert.IsFalse(scans.ObtainsTypesLocally(room));
        CollectionAssert.AreEquivalent(new[] { TechType.Titanium }, room.ShownTypes.ToArray());

        simulationOwnership.SimulateEntity(room.Id, SimulationLockType.TRANSIENT);
        Assert.IsTrue(scans.ObtainsTypesLocally(room));
    }

    [TestMethod]
    public void SnapshotWithoutTypesFromAServerThatWasNeverToldThemLeavesTheLocalListAlone()
    {
        room.ShownTypes = [TechType.Quartz];

        scans.ApplySnapshot(room, new MapRoomScanSnapshot(room.Id, 0, [], []));
        Assert.IsTrue(scans.ObtainsTypesLocally(room));
        CollectionAssert.AreEquivalent(new[] { TechType.Quartz }, room.ShownTypes.ToArray());

        // The simulating player has reported that nothing is in range.
        scans.ApplyTypes(room, new MapRoomScanTypesChanged(room.Id, []));
        Assert.AreEqual(0, room.ShownTypes.Count);
        scans.ApplySnapshot(room, new MapRoomScanSnapshot(room.Id, 0, [], [titanium]));
        CollectionAssert.AreEquivalent(new[] { TechType.Titanium }, room.ShownTypes.ToArray());
    }

    private List<T> Sent<T>() where T : Packet => packetSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<T>().ToList();

    private static ResourceTrackerDatabase.ResourceInfo Info(string id, float x, TechType techType = TechType.Quartz) => new()
    {
        uniqueId = id,
        techType = techType,
        // Measured from the scan origin of the fake room.
        position = new Vector3(1f + x, 2f, 3f)
    };

    /// <summary>
    ///     A room as the game has it, with the game's own parts of a scan reduced to what the rules can tell apart.
    /// </summary>
    private sealed class FakeRoom(NitroxId id, MapRoomScans scans) : MapRoomScans.IRoom
    {
        /// <summary>
        ///     What this client's game has loaded, of any type and at any distance.
        /// </summary>
        public List<ResourceTrackerDatabase.ResourceInfo> Loaded { get; } = [];

        public HashSet<TechType> LoadedTypes { get; } = [];
        public int ScansStarted { get; private set; }
        public int TargetsShown { get; private set; }
        public int ResultsShown { get; private set; }

        public NitroxId Id => id;
        public TechType Target { get; private set; }
        public int NodesScanned { get; set; }
        public List<ResourceTrackerDatabase.ResourceInfo> Nodes { get; } = [];
        public Vector3 ScanOrigin => new(1f, 2f, 3f);
        public float ScanRange { get; set; } = 300f;
        public bool Powered { get; set; } = true;
        public HashSet<TechType>? ShownTypes { get; set; } = [];
        public IEnumerable<TechType> DetectableTypes => [TechType.Quartz, TechType.Titanium];

        public void StartScanning(TechType target)
        {
            ScansStarted++;
            Target = target;
            ObtainNodes();
            NodesScanned = 0;
        }

        public void ObtainNodes()
        {
            // As the patched game does it.
            if (scans.ObtainsResultsLocally(this, Target))
            {
                Nodes.Clear();
                Nodes.AddRange(Loaded.Where(info => info.techType == Target));
            }
            scans.ResultsObtained(this);
        }

        public void ObtainTypes()
        {
            // As the patched game does it.
            if (scans.ObtainsTypesLocally(this))
            {
                ShownTypes = [.. LoadedTypes];
            }
            scans.ReportTypes(this);
        }

        public void ShowResults() => ResultsShown++;

        public void ShowTarget() => TargetsShown++;

        public void ShowTypes(IEnumerable<TechType> types) => ShownTypes = [.. types];
    }
}
