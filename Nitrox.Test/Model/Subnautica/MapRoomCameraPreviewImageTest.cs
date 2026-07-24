using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class MapRoomCameraPreviewImageTest
{
    [TestMethod]
    public void AcceptsBoundedBaselineJpeg()
    {
        byte[] jpeg = CreateJpeg(256, 192);

        Assert.IsTrue(MapRoomCameraPreviewImage.TryValidate(jpeg, out int width, out int height));
        Assert.AreEqual(256, width);
        Assert.AreEqual(192, height);
    }

    [DataTestMethod]
    [DataRow(257, 192)]
    [DataRow(256, 257)]
    [DataRow(0, 192)]
    [DataRow(192, 0)]
    public void RejectsInvalidDimensions(int width, int height)
    {
        Assert.IsFalse(MapRoomCameraPreviewImage.TryValidate(CreateJpeg(width, height), out _, out _));
    }

    [TestMethod]
    public void RejectsMissingOrTruncatedMarkers()
    {
        byte[] missingStart = CreateJpeg(64, 64);
        missingStart[0] = 0;
        byte[] missingEnd = CreateJpeg(64, 64);
        missingEnd[^1] = 0;
        byte[] truncatedSegment = CreateJpeg(64, 64);
        truncatedSegment[4] = 0x7F;

        Assert.IsFalse(MapRoomCameraPreviewImage.TryValidate(missingStart, out _, out _));
        Assert.IsFalse(MapRoomCameraPreviewImage.TryValidate(missingEnd, out _, out _));
        Assert.IsFalse(MapRoomCameraPreviewImage.TryValidate(truncatedSegment, out _, out _));
    }

    [TestMethod]
    public void RejectsPayloadOverByteCapBeforeParsing()
    {
        byte[] oversized = new byte[MapRoomCameraPreviewImage.MAX_ENCODED_BYTES + 1];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;
        oversized[^2] = 0xFF;
        oversized[^1] = 0xD9;

        Assert.IsFalse(MapRoomCameraPreviewImage.TryValidate(oversized, out _, out _));
    }

    internal static byte[] CreateJpeg(int width, int height) =>
    [
        0xFF, 0xD8,                         // SOI
        0xFF, 0xE0, 0x00, 0x02,             // empty APP segment
        0xFF, 0xC0, 0x00, 0x0B, 0x08,       // baseline SOF, one component
        (byte)(height >> 8), (byte)height,
        (byte)(width >> 8), (byte)width,
        0x01, 0x01, 0x11, 0x00,
        0xFF, 0xDA, 0x00, 0x08, 0x01,       // SOS, one component
        0x01, 0x00, 0x00, 0x3F, 0x00,
        0x00,                               // bounded entropy placeholder
        0xFF, 0xD9                          // EOI
    ];
}
