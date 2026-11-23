namespace Nitrox.Model.Subnautica.Helper;

/// <summary>
///     Bounds and validates the small JPEG payload used for the Scanner Room's session-only camera preview.
///     The parser deliberately stops at the start-of-scan marker; dimensions and total encoded size are the
///     security-relevant properties before a Unity client decodes the image.
/// </summary>
public static class MapRoomCameraPreviewImage
{
    /// <summary>
    ///     Width and height of the picture a client takes and the largest it accepts. A guess, not measured: the
    ///     picture is shown small on a room's screen.
    /// </summary>
    public const int MAX_DIMENSION = 256;

    /// <summary>
    ///     64 KiB. A guess at well above what a picture of <see cref="MAX_DIMENSION" /> needs, and little enough to
    ///     pass on to every player.
    /// </summary>
    public const int MAX_ENCODED_BYTES = 64 * 1024;

    /// <summary>
    ///     A first, loose bound that makes the reads of the first and last two bytes safe: no picture is this short
    ///     (the markers that open and close a file and one frame header are 17 bytes). The exact checks follow.
    /// </summary>
    private const int MIN_ENCODED_BYTES = 14;

    // The JPEG format, ITU-T T.81 Annex B. A marker is MARKER_PREFIX followed by one of the bytes below. All but the
    // ones noted are followed by a segment that starts with its own length in two bytes, high byte first.
    private const byte MARKER_PREFIX = 0xFF;
    private const int MARKER_LENGTH = 2;
    private const int SEGMENT_LENGTH_FIELD_SIZE = 2;

    /// <summary>
    ///     MARKER_PREFIX followed by this is a data byte of a scan and no marker. The parser stops before any scan.
    /// </summary>
    private const byte STUFFED_ZERO = 0x00;

    /// <summary>
    ///     TEM, a marker without a segment.
    /// </summary>
    private const byte TEMPORARY = 0x01;

    // SOF0 to SOF15. Three bytes of that range are other markers: DHT, JPG and DAC.
    private const byte FIRST_START_OF_FRAME = 0xC0;
    private const byte LAST_START_OF_FRAME = 0xCF;
    private const byte DEFINE_HUFFMAN_TABLE = 0xC4;
    private const byte JPEG_EXTENSION = 0xC8;
    private const byte DEFINE_ARITHMETIC_CODING = 0xCC;

    // RST0 to RST7, markers without a segment.
    private const byte FIRST_RESTART = 0xD0;
    private const byte LAST_RESTART = 0xD7;

    private const byte START_OF_IMAGE = 0xD8;
    private const byte END_OF_IMAGE = 0xD9;
    private const byte START_OF_SCAN = 0xDA;

    // A frame header: length (2 bytes), sample precision (1), height (2), width (2), number of components (1), then
    // 3 bytes for every component.
    private const int FRAME_HEIGHT_OFFSET = 3;
    private const int FRAME_WIDTH_OFFSET = 5;
    private const int FRAME_COMPONENT_COUNT_OFFSET = 7;
    private const int FRAME_FIXED_LENGTH = 8;
    private const int FRAME_BYTES_PER_COMPONENT = 3;

    // A scan header: length (2 bytes), number of components (1), 2 bytes for every component, then 3 more bytes.
    private const int SCAN_COMPONENT_COUNT_OFFSET = 2;
    private const int SCAN_FIXED_LENGTH = 6;
    private const int SCAN_BYTES_PER_COMPONENT = 2;

    // Grey has one component, colour three, CMYK four. The format allows more in a frame; no picture here has them.
    private const int MIN_COMPONENTS = 1;
    private const int MAX_COMPONENTS = 4;

    public static bool TryValidate(byte[] jpegBytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (jpegBytes == null || jpegBytes.Length < MIN_ENCODED_BYTES || jpegBytes.Length > MAX_ENCODED_BYTES ||
            jpegBytes[0] != MARKER_PREFIX || jpegBytes[1] != START_OF_IMAGE ||
            jpegBytes[^2] != MARKER_PREFIX || jpegBytes[^1] != END_OF_IMAGE)
        {
            return false;
        }

        bool foundStartOfFrame = false;
        int offset = MARKER_LENGTH;
        int encodedEnd = jpegBytes.Length - MARKER_LENGTH;
        while (offset < encodedEnd)
        {
            if (jpegBytes[offset++] != MARKER_PREFIX)
            {
                return false;
            }
            while (offset < encodedEnd && jpegBytes[offset] == MARKER_PREFIX)
            {
                offset++;
            }
            if (offset >= encodedEnd)
            {
                return false;
            }

            byte marker = jpegBytes[offset++];
            if (marker is STUFFED_ZERO or START_OF_IMAGE or END_OF_IMAGE)
            {
                return false;
            }
            if (marker == TEMPORARY || marker is >= FIRST_RESTART and <= LAST_RESTART)
            {
                continue;
            }
            if (!TryReadSegment(jpegBytes, offset, encodedEnd, out int segmentLength))
            {
                return false;
            }

            if (IsStartOfFrame(marker))
            {
                if (foundStartOfFrame || segmentLength < FRAME_FIXED_LENGTH + FRAME_BYTES_PER_COMPONENT * MIN_COMPONENTS)
                {
                    return false;
                }
                int componentCount = jpegBytes[offset + FRAME_COMPONENT_COUNT_OFFSET];
                if (componentCount is < MIN_COMPONENTS or > MAX_COMPONENTS || segmentLength != FRAME_FIXED_LENGTH + componentCount * FRAME_BYTES_PER_COMPONENT)
                {
                    return false;
                }
                height = ReadUInt16(jpegBytes, offset + FRAME_HEIGHT_OFFSET);
                width = ReadUInt16(jpegBytes, offset + FRAME_WIDTH_OFFSET);
                if (width is < 1 or > MAX_DIMENSION || height is < 1 or > MAX_DIMENSION)
                {
                    return false;
                }
                foundStartOfFrame = true;
            }
            else if (marker == START_OF_SCAN)
            {
                if (!foundStartOfFrame || segmentLength < SCAN_FIXED_LENGTH + SCAN_BYTES_PER_COMPONENT * MIN_COMPONENTS)
                {
                    return false;
                }
                int componentCount = jpegBytes[offset + SCAN_COMPONENT_COUNT_OFFSET];
                return componentCount is >= MIN_COMPONENTS and <= MAX_COMPONENTS && segmentLength == SCAN_FIXED_LENGTH + componentCount * SCAN_BYTES_PER_COMPONENT;
            }

            offset += segmentLength;
        }
        return false;
    }

    private static bool TryReadSegment(byte[] bytes, int offset, int encodedEnd, out int segmentLength)
    {
        segmentLength = 0;
        if (offset + SEGMENT_LENGTH_FIELD_SIZE > encodedEnd)
        {
            return false;
        }
        segmentLength = ReadUInt16(bytes, offset);
        return segmentLength >= SEGMENT_LENGTH_FIELD_SIZE && offset + segmentLength <= encodedEnd;
    }

    private static bool IsStartOfFrame(byte marker) =>
        marker is >= FIRST_START_OF_FRAME and <= LAST_START_OF_FRAME && marker is not DEFINE_HUFFMAN_TABLE and not JPEG_EXTENSION and not DEFINE_ARITHMETIC_CODING;

    private static int ReadUInt16(byte[] bytes, int offset) => bytes[offset] << 8 | bytes[offset + 1];
}
