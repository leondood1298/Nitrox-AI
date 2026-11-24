using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class LavaLarvaAttachmentsTest
{
    private static readonly NitroxVector3 leashPosition = new(-200f, -1200f, 600f);

    [TestMethod]
    public void LarvaOfAVehicleThatIsGoneLoadsLetGoKeepingTheRest()
    {
        EntityRegistry registry = new(NullLogger<EntityRegistry>.Instance);
        VehicleEntity prawn = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("Exosuit"), null);
        registry.AddEntity(prawn);
        WorldEntity onPrawn = AddLarva(registry, new LavaLarvaMetadata(true, prawn.Id, 2, 1f, hasLeash: true, leashPosition: leashPosition));
        WorldEntity onGoneSeamoth = AddLarva(registry, new LavaLarvaMetadata(true, new NitroxId(), 0, 1f, hasLeash: true, leashPosition: leashPosition));

        CollectionAssert.AreEqual(new[] { onGoneSeamoth }, LavaLarvaAttachments.DetachFromMissingVehicles(registry));

        Assert.IsTrue(((LavaLarvaMetadata)onPrawn.Metadata).Attached);
        LavaLarvaMetadata letGo = (LavaLarvaMetadata)onGoneSeamoth.Metadata;
        Assert.IsFalse(letGo.Attached);
        Assert.IsNull(letGo.VehicleId);
        Assert.AreEqual(1f, letGo.InfectedAmount);
        Assert.AreEqual(leashPosition, letGo.LeashPosition);
    }

    private static WorldEntity AddLarva(EntityRegistry registry, LavaLarvaMetadata state)
    {
        WorldEntity larva = new(new NitroxTransform(), 0, "", false, new NitroxId(), new NitroxTechType("LavaLarva"), state, null, []);
        registry.AddEntity(larva);
        return larva;
    }
}
