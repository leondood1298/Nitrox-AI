using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     Which of a camera's light, battery and health a client takes from the server and which it reports, and how fast
///     a docked camera is recharged, with a camera that needs no game.
/// </summary>
[TestClass]
public sealed class MapRoomCameraStatesTest
{
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private readonly SimulationOwnership simulationOwnership;
    private readonly MapRoomCameraStates states;
    private readonly FakeCamera camera = new(new NitroxId());

    public MapRoomCameraStatesTest()
    {
        simulationOwnership = new SimulationOwnership(Substitute.For<IMultiplayerSession>(), packetSender);
        states = new MapRoomCameraStates(packetSender, simulationOwnership, new ScannerRoomClientDiagnostics());
    }

    [TestMethod]
    public void StateOfTheServerIsPutIntoACameraThisClientDecidesNothingAbout()
    {
        states.Apply(camera, new MapRoomCameraMetadata(true, 40f, 300f, 2, 1));
        states.ReportIfChanged(camera);

        Assert.AreEqual((true, 40f, 300f), (camera.LightOn, camera.Energy, camera.Health));
        Assert.AreEqual(0, Reports().Count, "What came from the server is not reported back to it.");
    }

    [TestMethod]
    public void SimulatingPlayerDoesNotTakeItsOwnBatteryAndHealthBack()
    {
        states.Apply(camera, new MapRoomCameraMetadata(false, 100f, 400f, 2, 1));
        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.TRANSIENT);
        camera.Energy = 90f;
        states.ReportIfChanged(camera);
        // The game goes on draining the battery while the report is on its way.
        camera.Energy = 85f;
        camera.Health = 399.99f;

        // The server sends the camera's state for another reason, such as a dock it refused. It holds the report.
        states.Apply(camera, new MapRoomCameraMetadata(true, 90f, 400f, 2, MapRoomCameraMetadata.NOT_DOCKED));

        Assert.AreEqual((85f, 399.99f), (camera.Energy, camera.Health), "Its own report, put back into the game, would undo what the game did since.");
        Assert.AreEqual(1, camera.ComponentsApplied);
        Assert.IsTrue(camera.LightOn, "The light is not this player's to decide, so it is taken.");
    }

    [TestMethod]
    public void PlayerWhoFliesACameraDoesNotTakeItsLightBack()
    {
        states.Apply(camera, new MapRoomCameraMetadata(false, 100f, 400f, 2, 1));
        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.EXCLUSIVE);
        camera.LightOn = true;
        states.ReportIfChanged(camera);

        // A state the server sent before it had this report.
        states.Apply(camera, new MapRoomCameraMetadata(false, 100f, 400f, 2, 1));
        states.ReportIfChanged(camera);

        Assert.IsTrue(camera.LightOn);
        Assert.IsTrue(Reports().Single().LightOn, "Nor does it report the light again, which the server has by now.");
    }

    [TestMethod]
    public void RefusedReportIsCorrectedOnceThisClientKnowsItNoLongerSimulatesTheCamera()
    {
        states.Apply(camera, new MapRoomCameraMetadata(false, 100f, 400f, 2, 1));
        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.TRANSIENT);
        camera.Energy = 90f;
        states.ReportIfChanged(camera);

        // Another player took the camera over before the report arrived. The server says so first and then answers
        // the report it refused with what the camera really holds.
        simulationOwnership.StopSimulatingEntity(camera.Id);
        states.Apply(camera, new MapRoomCameraMetadata(false, 97f, 400f, 2, 1));

        Assert.AreEqual(97f, camera.Energy);
        camera.Energy = 50f;
        states.ReportIfChanged(camera);
        Assert.AreEqual(1, Reports().Count, "From then on it reports nothing.");
    }

    [TestMethod]
    public void NewObjectOfACameraTakesEverythingEvenFromThePlayerWhoSimulatesIt()
    {
        states.Apply(camera, new MapRoomCameraMetadata(false, 60f, 300f, 2, 1));
        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.EXCLUSIVE);
        // The camera was picked up and dropped: the game made a new object for it, which holds what its prefab came with.
        FakeCamera dropped = new(camera.Id) { Energy = 100f, Health = 400f };

        states.ReportIfChanged(dropped);
        Assert.AreEqual(0, Reports().Count, "What a prefab came with is no state of the camera.");

        states.Apply(dropped, new MapRoomCameraMetadata(true, 60f, 300f, 2, MapRoomCameraMetadata.NOT_DOCKED));

        Assert.AreEqual((true, 60f, 300f), (dropped.LightOn, dropped.Energy, dropped.Health));
    }

    [TestMethod]
    public void OnlyThePlayerWhoSimulatesACameraReportsItsBatteryAndHealthAndOnlyWhenTheyChangedEnough()
    {
        states.Apply(camera, new MapRoomCameraMetadata(false, 100f, 400f, 2, 1));
        camera.Energy = 50f;
        states.ReportIfChanged(camera);
        Assert.AreEqual(0, Reports().Count, "Another player simulates it.");

        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.TRANSIENT);
        camera.Energy = 99.6f;
        states.ReportIfChanged(camera);
        Assert.AreEqual(0, Reports().Count, "A recharging battery changes every frame.");

        camera.Energy = 99.4f;
        states.ReportIfChanged(camera);
        states.ReportIfChanged(camera);
        camera.Health = 399.9f;
        states.ReportIfChanged(camera);

        CollectionAssert.AreEqual(new[] { (99.4f, 400f), (99.4f, 399.9f) }, Reports().Select(report => (report.Energy, report.Health)).ToArray());
        Assert.AreEqual((2, 1), (Reports()[0].CameraNumber, Reports()[0].DockingIndex), "The number and the dock go back as the server gave them.");
    }

    [TestMethod]
    public void BatteryReportCarriesTheLightTheServerHasUnlessThisClientFliesTheCamera()
    {
        states.Apply(camera, new MapRoomCameraMetadata(false, 100f, 400f, 2, 1));
        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.TRANSIENT);
        // Whatever switched it on in this game, it is not this player's to tell the server.
        camera.LightOn = true;
        states.ReportIfChanged(camera);
        camera.Energy = 90f;
        states.ReportIfChanged(camera);

        Assert.IsFalse(Reports().Single().LightOn, "Sent as it is, the server would take the report for an attempt to switch the light.");

        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.EXCLUSIVE);
        states.ReportIfChanged(camera);

        Assert.IsTrue(Reports()[1].LightOn, "The player who flies the camera switches its light, and says so at once.");
    }

    [TestMethod]
    public void OnlyTheGameOfThePlayerWhoSimulatesACameraDamagesOrRepairsIt()
    {
        Assert.IsFalse(states.ChangesHealthHere(camera.Id, false), "Here the change would show in this game alone, until the server's state undoes it.");
        Assert.IsTrue(states.ChangesHealthHere(camera.Id, true), "What the server holds is put into every client's camera.");

        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.TRANSIENT);

        Assert.IsTrue(states.ChangesHealthHere(camera.Id, false));
    }

    [TestMethod]
    [DataRow(0f, 100f, 10f)]
    [DataRow(85f, 100f, 10f)]
    [DataRow(99.5f, 100f, 0.5f)]
    [DataRow(100f, 100f, 0f)]
    [DataRow(-5f, 100f, 10f)]
    [DataRow(0f, 0f, 0f)]
    public void DockChargeIsATenthOfCapacityPerUpdateLikeTheGame(float current, float capacity, float expected)
    {
        Assert.AreEqual(expected, MapRoomCameraStates.CalculateDockCharge(current, capacity), 0.0001f);
    }

    [TestMethod]
    [DataRow(40f, 100f, true, true)]
    [DataRow(99.5f, 100f, true, true)]
    [DataRow(99.6f, 100f, true, false)]
    [DataRow(100f, 100f, true, false)]
    [DataRow(40f, 100f, false, false)]
    [DataRow(0f, 0f, true, false)]
    public void CameraAnotherPlayerRechargesIsHeardChargingWhileItsReportedBatteryIsBelowFullAndItsBaseHasPower(float reportedEnergy, float capacity,
        bool basePowered, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraStates.IsRechargedByAnotherPlayer(reportedEnergy, capacity, basePowered));
    }

    [TestMethod]
    [DataRow(0f, true)]
    [DataRow(0.04f, true)]
    [DataRow(0.05f, false)]
    [DataRow(250f, false)]
    [DataRow(400f, false)]
    public void CameraRemovedByAnotherPlayerWasKilledOnlyIfItsLastReportedHealthIsZero(float reportedHealth, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraStates.WasKilled(reportedHealth));
    }

    [TestMethod]
    public void NothingIsReportedWhileTheBatteryIsBeingPutIn()
    {
        states.Apply(camera, new MapRoomCameraMetadata(false, 60f, 400f, 2, 1));
        simulationOwnership.SimulateEntity(camera.Id, SimulationLockType.TRANSIENT);
        // A camera without its battery shows no energy at all.
        camera.CanReport = false;
        camera.Energy = 0f;

        states.ReportIfChanged(camera);

        Assert.AreEqual(0, Reports().Count);
    }

    private List<MapRoomCameraMetadata> Reports() =>
        packetSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<EntityMetadataUpdate>().Select(update => (MapRoomCameraMetadata)update.NewValue).ToList();

    private sealed class FakeCamera(NitroxId id) : MapRoomCameraStates.ICamera
    {
        public int ComponentsApplied { get; private set; }

        public NitroxId Id => id;
        public object Body => this;
        public bool LightOn { get; set; }
        public float Energy { get; set; }
        public float Health { get; set; }
        public bool CanReport { get; set; } = true;

        public void ApplyComponents(float energy, float health)
        {
            Energy = energy;
            Health = health;
            ComponentsApplied++;
        }
    }
}
