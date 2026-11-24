using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

namespace BinaryPack.Serialization.Reflection
{
    internal static partial class KnownMembers
    {
        /// <summary>
        /// A <see langword="class"/> containing methods from the <see cref="Buffers.BinaryReader"/> type
        /// </summary>
        public static class BinaryReader
        {
            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.Read{T}()"/> method
            /// </summary>
            private static readonly MethodInfo _WriteT = (
                from method in typeof(Buffers.BinaryReader).GetMethods()
                where method.Name.Equals(nameof(Buffers.BinaryReader.Read))
                let parameters = method.GetParameters()
                let generics = method.GetGenericArguments()
                where parameters.Length == 0 &&
                      generics.Length == 1
                select method).First();

            /// <summary>
            /// Gets a generic <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.Read{T}()"/> method
            /// </summary>
            [Pure]
            public static MethodInfo ReadT(Type type) => _WriteT.MakeGenericMethod(type);

            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.Read{T}(Span{T})"/> method
            /// </summary>
            private static readonly MethodInfo _WriteSpanT = (
                from method in typeof(Buffers.BinaryReader).GetMethods()
                where method.Name.Equals(nameof(Buffers.BinaryReader.Read))
                let parameters = method.GetParameters()
                let generics = method.GetGenericArguments()
                where parameters.Length == 1 &&
                      generics.Length == 1 &&
                      parameters[0].ParameterType == typeof(Span<>).MakeGenericType(generics[0])
                select method).First();

            /// <summary>
            /// Gets a generic <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.Read{T}(Span{T})"/> method
            /// </summary>
            [Pure]
            public static MethodInfo ReadSpanT(Type type) => _WriteSpanT.MakeGenericMethod(type);

            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.ReadLength"/> method
            /// </summary>
            public static MethodInfo ReadLength { get; } = typeof(Buffers.BinaryReader).GetMethod(nameof(Buffers.BinaryReader.ReadLength));

            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.ReadBitLength"/> method
            /// </summary>
            public static MethodInfo ReadBitLength { get; } = typeof(Buffers.BinaryReader).GetMethod(nameof(Buffers.BinaryReader.ReadBitLength));

            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.ReadDimension"/> method
            /// </summary>
            public static MethodInfo ReadDimension { get; } = typeof(Buffers.BinaryReader).GetMethod(nameof(Buffers.BinaryReader.ReadDimension));

            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.CheckDimensions"/> method
            /// </summary>
            public static MethodInfo CheckDimensions { get; } = typeof(Buffers.BinaryReader).GetMethod(nameof(Buffers.BinaryReader.CheckDimensions));

            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.EnterNested"/> method
            /// </summary>
            public static MethodInfo EnterNested { get; } = typeof(Buffers.BinaryReader).GetMethod(nameof(Buffers.BinaryReader.EnterNested));

            /// <summary>
            /// The <see cref="MethodInfo"/> instance mapping the <see cref="Buffers.BinaryReader.ExitNested"/> method
            /// </summary>
            public static MethodInfo ExitNested { get; } = typeof(Buffers.BinaryReader).GetMethod(nameof(Buffers.BinaryReader.ExitNested));

            /// <summary>
            /// The fewest bytes one item of <paramref name="type"/> takes in serialized data
            /// </summary>
            /// <remarks>
            /// An unmanaged value is copied whole. Anything else is at least a null flag, a length or one serialized
            /// member, so at least a byte.
            /// </remarks>
            [Pure]
            public static int MinBytesPerItem(Type type)
            {
                if (type.IsUnmanaged())
                {
                    return (int)typeof(System.Runtime.CompilerServices.Unsafe).GetMethod(nameof(System.Runtime.CompilerServices.Unsafe.SizeOf)).MakeGenericMethod(type).Invoke(null, null);
                }
                return 1;
            }
        }
    }
}
