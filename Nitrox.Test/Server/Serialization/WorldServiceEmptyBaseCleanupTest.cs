using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Serialization.World;
using NitroxClient.GameLogic.Helper;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class WorldServiceEmptyBaseCleanupTest
{
    [TestMethod]
    public void RemovesOnlyChildlessStructurallyEmptyBuildEntities()
    {
        BuildEntity emptyBuild = CreateBuild("00000000-0000-0000-0000-000000000001", [0, 0, 0, 0]);
        BuildEntity occupiedBuild = CreateBuild("00000000-0000-0000-0000-000000000002", [0, 1, 0, 0]);
        BuildEntity emptyBuildWithChild = CreateBuild("00000000-0000-0000-0000-000000000003", [0, 0, 0, 0]);
        emptyBuildWithChild.ChildEntities.Add(InteriorPieceEntity.MakeEmpty());
        BuildEntity malformedBuild = BuildEntity.MakeEmpty();
        malformedBuild.Id = new NitroxId("00000000-0000-0000-0000-000000000004");
        malformedBuild.BaseData = new BaseData { PreCompressionSize = 4, Cells = [1, 2, 3] };

        GlobalRootData globalRootData = new()
        {
            Entities = [emptyBuild, occupiedBuild, emptyBuildWithChild, malformedBuild]
        };

        IReadOnlyList<BuildEntity> removed = WorldService.RemoveChildlessStructurallyEmptyBuildEntities(globalRootData);

        CollectionAssert.AreEqual(new[] { emptyBuild }, removed.ToArray());
        CollectionAssert.AreEquivalent(
            new GlobalRootEntity[] { occupiedBuild, emptyBuildWithChild, malformedBuild },
            globalRootData.Entities.ToArray());
    }

    private static BuildEntity CreateBuild(string id, byte[] cells)
    {
        BuildEntity buildEntity = BuildEntity.MakeEmpty();
        buildEntity.Id = new NitroxId(id);
        buildEntity.BaseData = new BaseData
        {
            PreCompressionSize = cells.Length,
            Cells = BaseSerializationHelper.CompressBytes(cells)
        };
        return buildEntity;
    }
}
