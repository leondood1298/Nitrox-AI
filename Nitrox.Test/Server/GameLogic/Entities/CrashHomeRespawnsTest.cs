using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

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

    private static WorldEntity NewEntity(string techType, EntityMetadata? metadata)
    {
        return new WorldEntity(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, techType, true, new NitroxId(), new NitroxTechType(techType), metadata,
                               null, []);
    }
}
