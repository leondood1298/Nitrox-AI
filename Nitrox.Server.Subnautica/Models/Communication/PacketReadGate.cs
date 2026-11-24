using System.Collections.Concurrent;
using System.Collections.Generic;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.Communication;

/// <summary>
///     Decides, from a packet's type and size alone, whether the server reads it at all. Anything on the open port can
///     send anything, before any password or name is checked; reading is where a hostile packet does its harm.
/// </summary>
/// <remarks>
///     <para>
///         A session that has not joined has only the anonymous processors to talk to (the session policy, the
///         reservation and the join request), so it may send only their packets, and only small ones. What it sends
///         that only a player may send was ignored after reading before; it is now ignored without reading.
///     </para>
///     <para>
///         A player's packet may be as large as its type ever needs: <see cref="DEFAULT_MAX_BYTES" />, or the size in
///         <see cref="MaxBytesOf" /> for the few types that carry pictures, bases or many entities. A larger one is
///         dropped and logged rather than read.
///     </para>
/// </remarks>
internal sealed class PacketReadGate(PacketRegistryService packetRegistryService, IOptions<SubnauticaServerOptions> options)
{
    /// <summary>The size a packet of a player may have unless its type is listed in <see cref="MaxBytesOf" />.</summary>
    public const int DEFAULT_MAX_BYTES = 64 * 1024;

    /// <summary>The size a packet of a session that has not joined may have; its packets hold a name, a key and a password.</summary>
    public const int NOT_JOINED_MAX_BYTES = 4 * 1024;

    /// <summary>Room for the ids and lengths around a packet's large part.</summary>
    private const int ENVELOPE_BYTES = 4 * 1024;

    private const int LARGE_MAX_BYTES = 1024 * 1024;
    private const int MEDIUM_MAX_BYTES = 256 * 1024;

    private static readonly Dictionary<Type, int> maxBytesByType = new()
    {
        // A base's cells and pieces, or an entity with all it holds
        [typeof(PlaceBase)] = LARGE_MAX_BYTES,
        [typeof(UpdateBase)] = LARGE_MAX_BYTES,
        [typeof(PlaceGhost)] = LARGE_MAX_BYTES,
        [typeof(PlaceModule)] = LARGE_MAX_BYTES,
        [typeof(PieceDeconstructed)] = LARGE_MAX_BYTES,
        [typeof(BaseDeconstructed)] = LARGE_MAX_BYTES,
        [typeof(WaterParkDeconstructed)] = LARGE_MAX_BYTES,
        [typeof(LargeWaterParkDeconstructed)] = LARGE_MAX_BYTES,
        [typeof(EntitySpawnedByClient)] = LARGE_MAX_BYTES,
        // Lists that grow with what a player sees or simulates
        [typeof(EntityTransformUpdates)] = MEDIUM_MAX_BYTES,
        [typeof(CellVisibilityChanged)] = MEDIUM_MAX_BYTES,
        [typeof(MapRoomScanReport)] = MEDIUM_MAX_BYTES,
        [typeof(MapRoomScanTypesReport)] = MEDIUM_MAX_BYTES,
        [typeof(MapRoomCameraPreview)] = MapRoomCameraPreviewImage.MAX_ENCODED_BYTES + ENVELOPE_BYTES
    };

    private readonly ConcurrentDictionary<Type, bool> hasAnonymousProcessorByType = new();

    public enum Verdict
    {
        /// <summary>Read and process it.</summary>
        READ,

        /// <summary>Leave it unread; the sender may stay.</summary>
        SKIP,

        /// <summary>Not a packet this server can accept from this sender; it is dropped.</summary>
        REFUSE
    }

    /// <param name="packetType">The type named in the packet's first bytes, or null if they name none.</param>
    /// <param name="length">The packet's size in bytes.</param>
    /// <param name="hasJoined">Whether the sender is a player.</param>
    public Verdict Check(Type? packetType, int length, bool hasJoined)
    {
        bool hasAnonymousProcessor = packetType != null &&
                                     hasAnonymousProcessorByType.GetOrAdd(packetType, type => typeof(IAnonPacketProcessor).IsAssignableFrom(packetRegistryService.GetProcessor(type).InterfaceType));
        return Decide(packetType, length, hasJoined, hasAnonymousProcessor, packetType == null ? 0 : MaxBytesOf(packetType));
    }

    /// <summary>
    ///     The largest packet of <paramref name="packetType" /> a player may send.
    /// </summary>
    public int MaxBytesOf(Type packetType)
    {
        if (packetType == typeof(PictureFrameUpload))
        {
            // The store refuses a picture over its own limit with a message the player sees; the gate must let it get there
            return (int)Math.Min(int.MaxValue, options.Value.PictureFrameMaxKilobytes * 1024L + ENVELOPE_BYTES);
        }
        return maxBytesByType.GetValueOrDefault(packetType, DEFAULT_MAX_BYTES);
    }

    internal static Verdict Decide(Type? packetType, int length, bool hasJoined, bool hasAnonymousProcessor, int maxBytes)
    {
        if (packetType == null)
        {
            return Verdict.REFUSE;
        }
        if (!hasJoined)
        {
            if (length > NOT_JOINED_MAX_BYTES)
            {
                return Verdict.REFUSE;
            }
            return hasAnonymousProcessor ? Verdict.READ : Verdict.SKIP;
        }
        return length > maxBytes ? Verdict.SKIP : Verdict.READ;
    }
}
