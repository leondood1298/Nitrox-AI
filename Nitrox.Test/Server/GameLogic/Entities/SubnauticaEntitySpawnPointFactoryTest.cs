using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Server.Subnautica.Models.UnityStubs;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class SubnauticaEntitySpawnPointFactoryTest
{
    private static readonly AbsoluteEntityCell cell = new(new NitroxInt3(11, 10, 11), new NitroxInt3(1, 2, 2), 2);
    private static readonly NitroxTransform transform = new(new NitroxVector3(1, 2, 3), NitroxQuaternion.Identity, NitroxVector3.One);

    [TestMethod]
    public void AGamesLightAtTheTopOfACellIsMadeOnce()
    {
        Dictionary<string, EntitySpawnPoint> spawnPointsById = [];

        List<EntitySpawnPoint> spawnPoints = new SubnauticaEntitySpawnPointFactory().From(cell, transform, NewLight("light", null), spawnPointsById);

        spawnPoints.Should().ContainSingle().Which.Should().BeOfType<SerializedEntitySpawnPoint>();
    }

    [TestMethod]
    public void AGamesLightUnderTheCellRootIsMadeOnlyAsAChildOfTheCellRoot()
    {
        SubnauticaEntitySpawnPointFactory factory = new();
        Dictionary<string, EntitySpawnPoint> spawnPointsById = [];
        List<EntitySpawnPoint> cellRoot = factory.From(cell, transform, NewObject("root", null, "55d7ab35-de97-4d95-af6c-ac8d03bb54ca"), spawnPointsById);

        List<EntitySpawnPoint> light = factory.From(cell, transform, NewLight("light", "root"), spawnPointsById);

        light.Should().BeEmpty();
        cellRoot.Single().Children.Should().ContainSingle().Which.Should().BeOfType<SerializedEntitySpawnPoint>();
    }

    [TestMethod]
    public void ParentsAreLookedUpOnlyAmongTheSpawnPointsOfTheSameParse()
    {
        SubnauticaEntitySpawnPointFactory factory = new();
        factory.From(cell, transform, NewObject("root", null, "rootClass"), []);

        List<EntitySpawnPoint> child = factory.From(cell, transform, NewObject("child", "root", "childClass"), []);

        // Nothing of an earlier batch is kept: the parent is not found
        child.Should().BeEmpty();
    }

    private static GameObject NewLight(string id, string? parent)
    {
        GameObject gameObject = new(new GameObjectData { CreateEmptyObject = true, Id = id, Parent = parent });
        gameObject.SerializedComponents.Add(new SerializedComponent("UnityEngine.Light", true, [1, 2, 3]));
        return gameObject;
    }

    private static GameObject NewObject(string id, string? parent, string classId)
    {
        return new GameObject(new GameObjectData { Id = id, Parent = parent, ClassId = classId });
    }
}
