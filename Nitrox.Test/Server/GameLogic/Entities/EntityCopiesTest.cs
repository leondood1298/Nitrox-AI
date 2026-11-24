using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class EntityCopiesTest
{
    [ClassInitialize]
    public static void Init(TestContext context) => Packet.InitSerializer();

    [TestMethod]
    public void ACopyHoldsTheSameHierarchyInObjectsOfItsOwn()
    {
        GlobalRootEntity root = Root();
        Entity locker = new PrefabChildEntity(new NitroxId(), "locker", NitroxTechType.None, 0, null, root.Id);
        Entity item = new PrefabChildEntity(new NitroxId(), "item", NitroxTechType.None, 1, null, locker.Id);
        root.ChildEntities.Add(locker);
        locker.ChildEntities.Add(item);

        List<GlobalRootEntity> copy = EntityCopies.Copy([root], out bool torn);

        torn.Should().BeFalse();
        copy.Should().ContainSingle().Which.Should().NotBeSameAs(root);
        Entity copiedLocker = copy[0].ChildEntities.Single();
        copiedLocker.Id.Should().Be(locker.Id);
        copiedLocker.Should().NotBeSameAs(locker);
        copiedLocker.ChildEntities.Single().Id.Should().Be(item.Id);

        // Changing the live hierarchy afterwards leaves the copy as it was
        locker.ChildEntities.Clear();
        copiedLocker.ChildEntities.Should().ContainSingle();
    }

    [TestMethod]
    public void AHierarchyWithAMissingChildIsNotWhole()
    {
        GlobalRootEntity root = Root();
        root.ChildEntities.Add(null!);

        EntityCopies.IsWhole([root]).Should().BeFalse();
        EntityCopies.IsWhole([Root()]).Should().BeTrue();
    }

    private static GlobalRootEntity Root() => new(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, "root", true, new NitroxId(), NitroxTechType.None, null, null, []);
}
