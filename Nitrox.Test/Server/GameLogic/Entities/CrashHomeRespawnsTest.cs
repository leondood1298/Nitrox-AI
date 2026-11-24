using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Networking;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;
using NSubstitute;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class CrashHomeRespawnsTest
{
    [TestMethod]
    public void AHomeWhoseCrashfishIsGoneMakesTheNextOneAfterTheGamesDelay()
    {
        WorldEntity crash = NewEntity("Crash", null);
        WorldEntity home = NewEntity("CrashHome", new CrashHomeMetadata(-1, crash.Id));
        WorldEntity otherHome = NewEntity("CrashHome", new CrashHomeMetadata(-1, new NitroxId()));

        List<Entity> released = CrashHomeRespawns.ReleaseHomesOf([crash, home, otherHome], crash.Id, 5000f);

        released.Should().ContainSingle().Which.Should().BeSameAs(home);
        CrashHomeMetadata metadata = home.Metadata.Should().BeOfType<CrashHomeMetadata>().Subject;
        metadata.SpawnTime.Should().Be(5000f + CrashHomeRespawns.RESPAWN_DELAY_SECONDS);
        metadata.SpawnedCrashId.HasValue.Should().BeFalse();
        ((CrashHomeMetadata)otherHome.Metadata).SpawnTime.Should().Be(-1);
    }

    [TestMethod]
    public void AHomeKeepsWaitingForItsCrashfishWhileTheFishIsInTheWorld()
    {
        WorldEntity crash = NewEntity("Crash", null);
        WorldEntity home = NewEntity("CrashHome", new CrashHomeMetadata(-1, crash.Id));

        CrashHomeRespawns.RepairAfterLoad([crash, home]).Should().Be(0);

        ((CrashHomeMetadata)home.Metadata).SpawnedCrashId.Value.Should().Be(crash.Id);
    }

    [TestMethod]
    public void AHomeLeftWithoutItsCrashfishByAnOlderBuildMakesOneAtOnce()
    {
        WorldEntity lostFish = NewEntity("CrashHome", new CrashHomeMetadata(-1, new NitroxId()));
        WorldEntity neverRecorded = NewEntity("CrashHome", new CrashHomeMetadata(-1, Optional.Empty));
        WorldEntity waiting = NewEntity("CrashHome", new CrashHomeMetadata(4000f, Optional.Empty));

        CrashHomeRespawns.RepairAfterLoad([lostFish, neverRecorded, waiting]).Should().Be(2);

        ((CrashHomeMetadata)lostFish.Metadata).SpawnTime.Should().Be(0);
        ((CrashHomeMetadata)neverRecorded.Metadata).SpawnTime.Should().Be(0);
        ((CrashHomeMetadata)waiting.Metadata).SpawnTime.Should().Be(4000f);
    }

    [TestMethod]
    public void ACrashfishThatLeavesTheRegistryInAnyWayReleasesItsHome()
    {
        (EntityRegistry registry, IPacketSender packetSender) = NewWorld(3000f);
        WorldEntity crash = NewEntity("Crash", null);
        WorldEntity home = NewEntity("CrashHome", new CrashHomeMetadata(-1, crash.Id));
        registry.AddEntities([crash, home]);

        // No packet processor in between: the registry is where every removal ends
        registry.RemoveEntity(crash.Id);

        CrashHomeMetadata metadata = home.Metadata.Should().BeOfType<CrashHomeMetadata>().Subject;
        metadata.SpawnTime.Should().Be(3000f + CrashHomeRespawns.RESPAWN_DELAY_SECONDS);
        metadata.SpawnedCrashId.HasValue.Should().BeFalse();
        packetSender.Received(1).SendPacketToAllAsync(Arg.Is<EntityMetadataUpdate>(update => update.Id.Equals(home.Id) && update.NewValue == home.Metadata));
    }

    [TestMethod]
    public void ACrashfishRemovedWithTheEntityItBelongedToReleasesItsHome()
    {
        (EntityRegistry registry, _) = NewWorld(5000f);
        WorldEntity parent = NewEntity("WaterPark", null);
        WorldEntity crash = NewEntity("Crash", null);
        crash.ParentId = parent.Id;
        parent.ChildEntities.Add(crash);
        WorldEntity home = NewEntity("CrashHome", new CrashHomeMetadata(-1, crash.Id));
        registry.AddEntities([parent, crash, home]);

        registry.RemoveEntity(parent.Id);

        ((CrashHomeMetadata)home.Metadata).SpawnTime.Should().Be(5000f + CrashHomeRespawns.RESPAWN_DELAY_SECONDS);
    }

    [TestMethod]
    public void RemovingAnythingElseLeavesTheHomeWaitingForItsCrashfish()
    {
        (EntityRegistry registry, IPacketSender packetSender) = NewWorld(5000f);
        WorldEntity crash = NewEntity("Crash", null);
        WorldEntity other = NewEntity("Peeper", null);
        WorldEntity home = NewEntity("CrashHome", new CrashHomeMetadata(-1, crash.Id));
        registry.AddEntities([crash, other, home]);

        registry.RemoveEntity(other.Id);

        ((CrashHomeMetadata)home.Metadata).SpawnedCrashId.Value.Should().Be(crash.Id);
        packetSender.DidNotReceiveWithAnyArgs().SendPacketToAllAsync<EntityMetadataUpdate>(default);
    }

    [TestMethod]
    public void AHomeNamingAnIdThatIsNoCrashfishMakesOneAtOnce()
    {
        WorldEntity notACrash = NewEntity("Peeper", null);
        WorldEntity home = NewEntity("CrashHome", new CrashHomeMetadata(-1, notACrash.Id));

        CrashHomeRespawns.RepairAfterLoad([notACrash, home]).Should().Be(1);

        ((CrashHomeMetadata)home.Metadata).SpawnTime.Should().Be(0);
    }

    /// <summary>
    ///     A registry with the crash homes' server side listening to it, as the server has it from startup.
    /// </summary>
    private static (EntityRegistry Registry, IPacketSender PacketSender) NewWorld(float gameTimeSeconds)
    {
        EntityRegistry registry = new(NullLogger<EntityRegistry>.Instance);
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        TimeService timeService = new(packetSender, new NtpSyncer(), NullLoggerFactory.Instance, NullLogger<TimeService>.Instance)
        {
            GameTime = TimeSpan.FromSeconds(gameTimeSeconds)
        };
        _ = new CrashHomeRespawns(registry, timeService, packetSender, NullLogger<CrashHomeRespawns>.Instance);
        return (registry, packetSender);
    }

    private static WorldEntity NewEntity(string techType, EntityMetadata? metadata)
    {
        return new WorldEntity(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, techType, true, new NitroxId(), new NitroxTechType(techType), metadata,
                               null, []);
    }
}
