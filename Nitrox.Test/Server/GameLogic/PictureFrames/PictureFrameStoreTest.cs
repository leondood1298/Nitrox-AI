using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic.PictureFrames;

[TestClass]
public sealed class PictureFrameStoreTest
{
    private static readonly NitroxId frameA = new("00000000-0000-0000-0000-000000000b01");
    private static readonly NitroxId frameB = new("00000000-0000-0000-0000-000000000b02");

    private string directory = null!;
    private EntityRegistry registry = null!;

    [TestInitialize]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), $"nitrox-pictures-{Guid.NewGuid():N}");
        registry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        registry.AddEntity(Frame(frameA));
        registry.AddEntity(Frame(frameB));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void PictureIsKeptWithTheWorldAndNamedByTheFrame()
    {
        PictureFrameStore store = Store();
        byte[] picture = Picture(100, 1);

        Assert.IsNull(store.TryStore(frameA, picture, out string hash, out _));

        Assert.AreEqual(hash, ((PictureFrameMetadata)registry.GetEntityById(frameA).Value.Metadata).PictureHash);
        CollectionAssert.AreEqual(picture, store.GetPictureOf(frameA, hash));
        Assert.IsTrue(File.Exists(Path.Combine(directory, $"PictureFrame-{hash}.jpg")), "Pictures are files of the world's folder, which its backups copy.");
    }

    [TestMethod]
    public void PictureLargerThanOnePictureMayBeIsRefused()
    {
        PictureFrameStore store = Store(maxKilobytes: 1);

        Assert.AreEqual(PictureFrameRefusal.TOO_LARGE, store.TryStore(frameA, Picture(2048, 1), out _, out long limit));
        Assert.AreEqual(1024, limit);
        Assert.IsNull(registry.GetEntityById(frameA).Value.Metadata);
    }

    [TestMethod]
    public void PictureBeyondTheWorldsRoomIsRefused()
    {
        PictureFrameStore store = Store(maxKilobytes: 1024, maxTotalMegabytes: 1);
        Assert.IsNull(store.TryStore(frameA, Picture(700 * 1024, 1), out _, out _));

        Assert.AreEqual(PictureFrameRefusal.WORLD_FULL, store.TryStore(frameB, Picture(700 * 1024, 2), out _, out long limit));
        Assert.AreEqual(1024 * 1024 - 700 * 1024, limit);
    }

    /// <summary>
    ///     A deconstructed frame no longer holds the world's room for pictures.
    /// </summary>
    [TestMethod]
    public void DeconstructedFrameFreesItsPicture()
    {
        PictureFrameStore store = Store(maxKilobytes: 1024, maxTotalMegabytes: 1);
        Assert.IsNull(store.TryStore(frameA, Picture(700 * 1024, 1), out string firstHash, out _));
        registry.RemoveEntity(frameA);

        Assert.AreEqual(0, store.UsedBytes());
        Assert.IsNull(store.TryStore(frameB, Picture(700 * 1024, 2), out _, out _));
        Assert.IsFalse(File.Exists(Path.Combine(directory, $"PictureFrame-{firstHash}.jpg")), "The picture no frame shows is removed with the next one stored.");
    }

    [TestMethod]
    public void ReplacedPictureIsRemovedAndTheSameOneIsKeptOnce()
    {
        PictureFrameStore store = Store();
        Assert.IsNull(store.TryStore(frameA, Picture(100, 1), out string oldHash, out _));
        Assert.IsNull(store.TryStore(frameB, Picture(100, 2), out string sharedHash, out _));

        Assert.IsNull(store.TryStore(frameA, Picture(100, 2), out string newHash, out _));

        Assert.AreEqual(sharedHash, newHash);
        Assert.IsFalse(File.Exists(Path.Combine(directory, $"PictureFrame-{oldHash}.jpg")));
        Assert.AreEqual(100, store.UsedBytes());
    }

    [TestMethod]
    public void OnlyThePictureAFrameShowsIsGiven()
    {
        PictureFrameStore store = Store();
        Assert.IsNull(store.TryStore(frameA, Picture(100, 1), out string hash, out _));

        Assert.IsNull(store.GetPictureOf(frameB, hash));
        Assert.IsNull(store.GetPictureOf(frameA, "../../etc"));
    }

    [TestMethod]
    public void OnlyAPictureFrameTakesAPicture()
    {
        NitroxId lockerId = new("00000000-0000-0000-0000-000000000b03");
        ModuleEntity locker = ModuleEntity.MakeEmpty();
        locker.Id = lockerId;
        locker.TechType = new NitroxTechType("SmallLocker");
        registry.AddEntity(locker);

        Assert.AreEqual(PictureFrameRefusal.NO_FRAME, Store().TryStore(lockerId, Picture(100, 1), out _, out _));
    }

    private PictureFrameStore Store(int maxKilobytes = 2048, int maxTotalMegabytes = 64) =>
        new(registry, new PictureFrameFiles(() => directory),
            Options.Create(new SubnauticaServerOptions { PictureFrameMaxKilobytes = maxKilobytes, PictureFramesMaxTotalMegabytes = maxTotalMegabytes }),
            NullLogger<PictureFrameStore>.Instance);

    private static ModuleEntity Frame(NitroxId id)
    {
        ModuleEntity frame = ModuleEntity.MakeEmpty();
        frame.Id = id;
        frame.TechType = new NitroxTechType("PictureFrame");
        return frame;
    }

    private static byte[] Picture(int size, byte fill)
    {
        byte[] picture = new byte[size];
        Array.Fill(picture, fill);
        return picture;
    }
}
