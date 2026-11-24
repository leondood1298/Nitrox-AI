using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BinaryPack.Serialization.Buffers
{
    /// <summary>
    /// A <see langword="struct"/> that provides a fast implementation of a binary reader, pulling data from a given <see cref="Span{T}"/>
    /// </summary>
    /// <remarks>
    /// The data can come from another machine, so nothing it says is trusted: a length must fit in the bytes that are
    /// left (<see cref="ReadLength"/>), objects may only nest <see cref="MaxDepth"/> deep (<see cref="EnterNested"/>), and
    /// no read goes past the end of the buffer. Each refusal is an <see cref="InvalidDataException"/>.
    /// </remarks>
    internal ref struct BinaryReader
    {
        /// <summary>
        /// How deep objects may nest. Nitrox's deepest real data (an item in a container in a module of a base, or a
        /// vehicle docked in a Cyclops) stays far below this; a hostile chain would otherwise exhaust the thread's stack,
        /// which cannot be caught and ends the process.
        /// </summary>
        public const int MaxDepth = 128;

        /// <summary>
        /// The <see cref="Span{T}"/> current in use
        /// </summary>
        private readonly ReadOnlySpan<byte> Buffer;

        /// <summary>
        /// The current position into <see cref="Buffer"/>
        /// </summary>
        private int _Position;

        /// <summary>
        /// How many objects are being read inside one another right now
        /// </summary>
        private int _Depth;

        /// <summary>
        /// Creates a new <see cref="BinaryReader"/> instance with the given parameters
        /// </summary>
        /// <param name="buffer">The source<see cref="Span{T}"/> to read data from</param>
        public BinaryReader(ReadOnlySpan<byte> buffer)
        {
            Buffer = buffer;
            _Position = 0;
            _Depth = 0;
        }

        /// <summary>
        /// The number of bytes not read yet
        /// </summary>
        public int Remaining => Buffer.Length - _Position;

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the underlying buffer
        /// </summary>
        /// <typeparam name="T">The type of value to read from the buffer</typeparam>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>() where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            // Indexing the span only checks the first byte; a value at the very end would be read partly past it
            if (size > Buffer.Length - _Position)
            {
                ThrowPastEnd(size);
            }

            /* The reasons for declaring this struct as a ref struct, and for
             * carrying a Span<byte> instead of a byte[] array are that this way
             * the reader can also read data from memory areas that are now owned
             * by the caller, or data that is just a slice on another array.
             * These variable declarations are just for clarity, they are
             * all optimized away bit the JIT compiler anyway. */
            ref byte r0 = ref Unsafe.AsRef(Buffer[_Position]);
            T value = Unsafe.As<byte, T>(ref r0);
            _Position += size;

            return value;
        }

        /// <summary>
        /// Reads a sequence of elements of type <typeparamref name="T"/> from the underlying buffer
        /// </summary>
        /// <typeparam name="T">The type of elements to read from the buffer</typeparam>
        /// <param name="span">The target <see cref="Span{T}"/> to write the read elements to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<T>(Span<T> span) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>() * span.Length;
            if (size < 0 || size > Buffer.Length - _Position)
            {
                ThrowPastEnd(size);
            }
            Span<byte> destination = MemoryMarshal.Cast<T, byte>(span);
            ReadOnlySpan<byte> source = Buffer.Slice(_Position, size);

            source.CopyTo(destination);
            _Position += size;
        }

        /// <summary>
        /// Reads the length of a string, array or collection: -1 for <see langword="null"/>, else the number of items.
        /// </summary>
        /// <param name="minBytesPerItem">The fewest bytes one item takes in the data</param>
        /// <exception cref="InvalidDataException">The length is negative or more items than the bytes left can hold.</exception>
        /// <remarks>
        /// Checked before anything is allocated for the items: a few bytes could otherwise ask for gigabytes.
        /// </remarks>
        public int ReadLength(int minBytesPerItem)
        {
            int length = Read<int>();
            if (length < -1 || (long)length * minBytesPerItem > Buffer.Length - _Position)
            {
                throw new InvalidDataException($"A length of {length} does not fit the {Buffer.Length - _Position} bytes left");
            }
            return length;
        }

        /// <summary>
        /// Reads the length of a bit array in bits: -1 for <see langword="null"/>, else the number of bits
        /// </summary>
        /// <exception cref="InvalidDataException">The length is negative or more bits than the bytes left can hold.</exception>
        public int ReadBitLength()
        {
            int length = Read<int>();
            if (length < -1 || length > (long)(Buffer.Length - _Position) * 8)
            {
                throw new InvalidDataException($"A bit array of {length} bits does not fit the {Buffer.Length - _Position} bytes left");
            }
            return length;
        }

        /// <summary>
        /// Reads one dimension of a multidimensional array whose items number <paramref name="length"/>
        /// </summary>
        /// <param name="productSoFar">The dimensions read before this one, multiplied</param>
        /// <exception cref="InvalidDataException">The dimensions would hold more items than the length says.</exception>
        public int ReadDimension(int length, int productSoFar)
        {
            int dimension = Read<int>();
            if (dimension < 0 || (length > 0 && (long)productSoFar * dimension > length))
            {
                throw new InvalidDataException($"An array dimension of {dimension} does not fit a length of {length}");
            }
            return dimension;
        }

        /// <summary>
        /// Checks that the dimensions of a multidimensional array hold exactly the number of items read for it
        /// </summary>
        /// <exception cref="InvalidDataException">They do not.</exception>
        public static void CheckDimensions(int length, int product)
        {
            if (product != length)
            {
                throw new InvalidDataException($"Array dimensions holding {product} items do not match its length of {length}");
            }
        }

        /// <summary>
        /// Called when reading an object starts
        /// </summary>
        /// <exception cref="InvalidDataException">Objects nest deeper than <see cref="MaxDepth"/>.</exception>
        public void EnterNested()
        {
            if (++_Depth > MaxDepth)
            {
                throw new InvalidDataException($"Objects nest deeper than {MaxDepth}");
            }
        }

        /// <summary>
        /// Called when reading an object is done
        /// </summary>
        public void ExitNested() => _Depth--;

        private void ThrowPastEnd(int size) => throw new InvalidDataException($"Reading {size} bytes at {_Position} goes past the end of the {Buffer.Length} bytes");
    }
}
