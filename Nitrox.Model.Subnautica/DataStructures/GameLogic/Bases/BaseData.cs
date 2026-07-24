using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;

[DataContract]
public class BaseData : IEquatable<BaseData>
{
    // Far above practical base sizes, while preventing untrusted save or packet data from
    // driving the multi-array allocations in BuildEntitySpawner.ApplyBaseData without bound.
    public const int MAX_CELL_COUNT = 1_000_000;

    [DataMember(Order = 1)]
    public NitroxInt3 BaseShape;

    [DataMember(Order = 2)]
    public NitroxInt3 CellOffset;

    [DataMember(Order = 3)]
    public NitroxInt3 Anchor;

    [DataMember(Order = 4)]
    public int PreCompressionSize;

    [DataMember(Order = 5)]
    public byte[] Faces;

    [DataMember(Order = 6)]
    public byte[] Cells;

    [DataMember(Order = 7)]
    public byte[] Links;

    [DataMember(Order = 8)]
    public byte[] Masks;

    [DataMember(Order = 9)]
    public byte[] IsGlass;

    /// <summary>
    /// Validates the compressed cell stream and reports whether at least one base cell is occupied.
    /// This reads the run-length stream without allocating an array based on save or packet data.
    /// </summary>
    public bool TryHasOccupiedCell(out bool hasOccupiedCell)
    {
        hasOccupiedCell = false;
        if (PreCompressionSize is <= 0 or > MAX_CELL_COUNT || Cells == null || Cells.Length == 0)
        {
            return false;
        }

        try
        {
            using MemoryStream input = new(Cells);
            using DeflateStream stream = new(input, CompressionMode.Decompress);
            using BinaryReader reader = new(stream);

            int decodedCells = 0;
            bool readingZeroRun = true;
            while (decodedCells < PreCompressionSize)
            {
                if (readingZeroRun)
                {
                    ushort zeroRunLength = reader.ReadUInt16();
                    if (zeroRunLength > PreCompressionSize - decodedCells)
                    {
                        hasOccupiedCell = false;
                        return false;
                    }
                    decodedCells += zeroRunLength;
                }
                else
                {
                    hasOccupiedCell |= reader.ReadByte() != 0;
                    decodedCells++;
                }

                readingZeroRun = !readingZeroRun;
            }

            // A valid stream describes exactly PreCompressionSize cells.
            if (stream.ReadByte() != -1)
            {
                hasOccupiedCell = false;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            hasOccupiedCell = false;
            return false;
        }
    }

    public bool Equals(BaseData other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return BaseShape.Equals(other.BaseShape) &&
               CellOffset.Equals(other.CellOffset) &&
               Anchor.Equals(other.Anchor) &&
               PreCompressionSize == other.PreCompressionSize &&
               Faces.SequenceEqual(other.Faces) &&
               Cells.SequenceEqual(other.Cells) &&
               Links.SequenceEqual(other.Links) &&
               Masks.SequenceEqualOrBothNull(other.Masks) &&
               IsGlass.SequenceEqual(other.IsGlass);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((BaseData)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = BaseShape.GetHashCode();
            hashCode = (hashCode * 397) ^ CellOffset.GetHashCode();
            hashCode = (hashCode * 397) ^ Anchor.GetHashCode();
            hashCode = (hashCode * 397) ^ PreCompressionSize;
            hashCode = (hashCode * 397) ^ (Faces != null ? Faces.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Cells != null ? Cells.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Links != null ? Links.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Masks != null ? Masks.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (IsGlass != null ? IsGlass.GetHashCode() : 0);
            return hashCode;
        }
    }
}
