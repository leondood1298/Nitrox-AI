namespace Nitrox.Model.Subnautica.Helper;

/// <summary>
///     Bounds and validates the small JPEG payload used for the Scanner Room's session-only camera preview.
///     The parser deliberately stops at the start-of-scan marker; dimensions and total encoded size are the
///     security-relevant properties before a Unity client decodes the image.
/// </summary>
public static class MapRoomCameraPreviewImage
{
    public const int MAX_DIMENSION = 256;
    public const int MAX_ENCODED_BYTES = 64 * 1024;

    public static bool TryValidate(byte[] jpegBytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (jpegBytes == null || jpegBytes.Length < 14 || jpegBytes.Length > MAX_ENCODED_BYTES ||
            jpegBytes[0] != 0xFF || jpegBytes[1] != 0xD8 ||
            jpegBytes[^2] != 0xFF || jpegBytes[^1] != 0xD9)
        {
            return false;
        }

        bool foundStartOfFrame = false;
        int offset = 2;
        int encodedEnd = jpegBytes.Length - 2;
        while (offset < encodedEnd)
        {
            if (jpegBytes[offset++] != 0xFF)
            {
                return false;
            }
            while (offset < encodedEnd && jpegBytes[offset] == 0xFF)
            {
                offset++;
            }
            if (offset >= encodedEnd)
            {
                return false;
            }

            byte marker = jpegBytes[offset++];
            if (marker is 0x00 or 0xD8 or 0xD9)
            {
                return false;
            }
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }
            if (!TryReadSegment(jpegBytes, offset, encodedEnd, out int segmentLength))
            {
                return false;
            }

            if (IsStartOfFrame(marker))
            {
                if (foundStartOfFrame || segmentLength < 11)
                {
                    return false;
                }
                int componentCount = jpegBytes[offset + 7];
                if (componentCount is < 1 or > 4 || segmentLength != 8 + componentCount * 3)
                {
                    return false;
                }
                height = ReadUInt16(jpegBytes, offset + 3);
                width = ReadUInt16(jpegBytes, offset + 5);
                if (width is < 1 or > MAX_DIMENSION || height is < 1 or > MAX_DIMENSION)
                {
                    return false;
                }
                foundStartOfFrame = true;
            }
            else if (marker == 0xDA)
            {
                if (!foundStartOfFrame || segmentLength < 8)
                {
                    return false;
                }
                int componentCount = jpegBytes[offset + 2];
                return componentCount is >= 1 and <= 4 && segmentLength == 6 + componentCount * 2;
            }

            offset += segmentLength;
        }
        return false;
    }

    private static bool TryReadSegment(byte[] bytes, int offset, int encodedEnd, out int segmentLength)
    {
        segmentLength = 0;
        if (offset + 2 > encodedEnd)
        {
            return false;
        }
        segmentLength = ReadUInt16(bytes, offset);
        return segmentLength >= 2 && offset + segmentLength <= encodedEnd;
    }

    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xCF && marker is not 0xC4 and not 0xC8 and not 0xCC;

    private static int ReadUInt16(byte[] bytes, int offset) => bytes[offset] << 8 | bytes[offset + 1];
}
