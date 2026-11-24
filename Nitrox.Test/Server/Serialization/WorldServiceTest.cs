using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Serialization;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class WorldServiceTest
{
    private string folder = null!;

    [TestInitialize]
    public void Setup()
    {
        folder = Path.Combine(Path.GetTempPath(), $"nitrox-version-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(folder, true);

    [TestMethod]
    public void VersionFileOfThisBuildHasTheCurrentSaveFormat()
    {
        string path = Path.Combine(folder, "Version.json");
        new ServerJsonSerializer(NullLogger<ServerJsonSerializer>.Instance).Serialize(path, new SaveFileVersion());

        WorldService.TryReadSaveFormat(path).Should().Be(NitroxSaveFormat.CURRENT);
    }

    [TestMethod]
    public void VersionFileWithoutSaveFormatIsFromAnOlderBuild()
    {
        string path = Path.Combine(folder, "Version.json");
        File.WriteAllText(path, """{"Major":1,"Minor":9,"Build":0,"Revision":0}""");

        WorldService.TryReadSaveFormat(path).Should().Be(NitroxSaveFormat.UNKNOWN);
    }

    [TestMethod]
    public void UnreadableVersionFileHasNoFormat()
    {
        string path = Path.Combine(folder, "Version.json");
        File.WriteAllText(path, "{ cut");

        WorldService.TryReadSaveFormat(path).Should().BeNull();
    }

    [TestMethod]
    public void EntitiesSavedWithTransformsThatAreNotFiniteAreGivenFiniteOnesExceptScannerRoomCameras()
    {
        NitroxVector3 notANumber = new(float.NaN, float.NaN, float.NaN);
        GlobalRootEntity vehicle = new(new NitroxTransform(notANumber, NitroxQuaternion.Identity, NitroxVector3.One), GlobalRootEntity.GLOBAL_ROOT_LEVEL, "seamoth", true, new NitroxId(),
            new NitroxTechType("Seamoth"), null, null, []);
        GlobalRootEntity child = new(new NitroxTransform(new NitroxVector3(1, 2, 3), new NitroxQuaternion(float.NaN, 0, 0, 1), NitroxVector3.One), GlobalRootEntity.GLOBAL_ROOT_LEVEL, "item", true,
            new NitroxId(), new NitroxTechType("Titanium"), null, vehicle.Id, []);
        vehicle.ChildEntities.Add(child);
        GlobalRootEntity camera = new(new NitroxTransform(notANumber, NitroxQuaternion.Identity, NitroxVector3.One), GlobalRootEntity.GLOBAL_ROOT_LEVEL, "camera", true, new NitroxId(),
            new NitroxTechType("MapRoomCamera"), null, null, []);

        List<(WorldEntity Entity, string Place)> healed = WorldService.HealTransformsThatAreNotFinite([vehicle, camera]);

        healed.Select(entry => entry.Entity).Should().Equal(vehicle, child);
        vehicle.Transform.LocalPosition.Should().Be(NitroxVector3.Zero);
        child.Transform.LocalPosition.Should().Be(new NitroxVector3(1, 2, 3), "only what is not a number is replaced");
        child.Transform.LocalRotation.Should().Be(NitroxQuaternion.Identity);
        camera.Transform.IsFinite().Should().BeFalse("the Scanner Room manager puts a camera into a free dock instead");
    }
}
