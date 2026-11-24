using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BinaryPack;
using BinaryPack.Attributes;
using Nitrox.Model.Networking;

namespace Nitrox.Model.Packets
{
    [Serializable]
    public abstract class Packet
    {
        private static readonly Dictionary<Type, PropertyInfo[]> cachedPropertiesByType = new();
        private static readonly LockObject cachedPropertiesByTypeLocker = new();

        [ThreadStatic]
        private static StringBuilder? toStringBuilder;

        private static readonly LockObject lockObject = new();

        [IgnoredMember]
        public NitroxDeliveryMethod.DeliveryMethod DeliveryMethod { get; protected set; } = NitroxDeliveryMethod.DeliveryMethod.RELIABLE_ORDERED;

        [IgnoredMember]
        public UdpChannelId UdpChannel { get; protected set; } = UdpChannelId.DEFAULT;

        public static void InitSerializer()
        {
            static IEnumerable<Type> FindTypesInModelAssemblies()
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                                .Where(assembly => new[] { $"{nameof(Nitrox)}.{nameof(Model)}", "Nitrox.Model.Subnautica" }
                                           .Contains(assembly.GetName().Name))
                                .SelectMany(assembly =>
                                {
                                    try
                                    {
                                        return assembly.GetTypes();
                                    }
                                    catch (ReflectionTypeLoadException e)
                                    {
                                        return e.Types.Where(t => t != null);
                                    }
                                });
            }

            static IEnumerable<Type> FindUnionBaseTypes() => FindTypesInModelAssemblies()
                .Where(t => t.IsAbstract && !t.IsSealed && (!t.BaseType?.IsAbstract ?? true) && !t.ContainsGenericParameters);

            lock (lockObject)
            {
                foreach (Type type in FindUnionBaseTypes())
                {
                    Type[] unionTypes = FindTypesInModelAssemblies()
                                        .Where(t => type.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                                        .OrderByDescending(t =>
                                        {
                                            Type current = t;
                                            int levels = 0;

                                            while (current != type && current != null)
                                            {
                                                current = current.BaseType;
                                                levels++;
                                            }

                                            return levels;
                                        })
                                        .ThenBy(t => t.FullName, StringComparer.Ordinal)
                                        .ToArray();
                    BinaryConverter.RegisterUnion(type, unionTypes);
                    if (type == typeof(Packet))
                    {
                        packetTypesByUnionIndex = unionTypes;
                    }
                }

                // This will initialize the processor for Wrapper which will initialize all the others
                _ = BinaryConverter.Serialize(new Wrapper());
            }
        }

        public byte[] Serialize()
        {
            return BinaryConverter.Serialize(new Wrapper(this));
        }

        public void SerializeInto(Stream stream)
        {
            BinaryConverter.Serialize(new Wrapper(this), stream);
        }

        public static Packet? Deserialize(byte[] data) => BinaryConverter.Deserialize<Wrapper>(data).Packet;

        /// <summary>
        ///     Reads a packet from the first <paramref name="length" /> bytes of <paramref name="data" />, which may be a
        ///     larger pooled buffer. Lengths and nesting inside the packet are held to those bytes.
        /// </summary>
        /// <exception cref="InvalidDataException">The data is not a packet that fits in it.</exception>
        public static Packet? Deserialize(byte[] data, int length) => BinaryConverter.Deserialize<Wrapper>(new ReadOnlySpan<byte>(data, 0, length)).Packet;

        /// <summary>
        ///     The packet types in the order of the index the serializer writes before each packet.
        /// </summary>
        private static Type[] packetTypesByUnionIndex = [];

        /// <summary>
        ///     Which type of packet serialized data holds, read from its first bytes without reading the packet.
        /// </summary>
        /// <returns>False if the data is too short or names no packet type.</returns>
        public static bool TryPeekType(ReadOnlySpan<byte> data, out Type? packetType)
        {
            packetType = null;
            if (data.Length < sizeof(int))
            {
                return false;
            }
            // The serialized Wrapper starts with its packet: the index of the packet's type in the union
            int index = data[0] | data[1] << 8 | data[2] << 16 | data[3] << 24;
            Type[] types = packetTypesByUnionIndex;
            if (index < 0 || index >= types.Length)
            {
                return false;
            }
            packetType = types[index];
            return true;
        }

        public override string ToString()
        {
            Type packetType = GetType();

            PropertyInfo[] properties;
            lock (cachedPropertiesByTypeLocker)
            {
                if (!cachedPropertiesByType.TryGetValue(packetType, out properties))
                {
                    properties = packetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                           .Where(x => x.Name is not nameof(DeliveryMethod) and not nameof(UdpChannel)).ToArray();
                    cachedPropertiesByType.Add(packetType, properties);
                }
            }

            toStringBuilder ??= new();
            toStringBuilder.Clear();
            toStringBuilder.Append($"[{packetType.Name}: ");
            foreach (PropertyInfo property in properties)
            {
                object propertyValue = property.GetValue(this);
                if (propertyValue is IList propertyList)
                {
                    toStringBuilder.Append($"{property.Name}: {propertyList.Count}, ");
                }
                else
                {
                    toStringBuilder.Append($"{property.Name}: {propertyValue}, ");
                }
            }

            toStringBuilder.Remove(toStringBuilder.Length - 2, 2);
            toStringBuilder.Append(']');

            return toStringBuilder.ToString();
        }

        /// <summary>
        ///     Wrapper which is used to serialize packets in BinaryPack.
        ///     We cannot serialize Packets directly because
        ///     <p>
        ///         1) We will not know what type to deserialize to and
        ///         2) The root object must have a callable constructor so it can't be abstract
        ///     </p>
        ///     This type solves both problems and only adds a single byte to the data.
        /// </summary>
        public readonly struct Wrapper(Packet packet)
        {
            public Packet? Packet { get; init; } = packet;
        }

        public enum UdpChannelId : byte
        {
            DEFAULT = 0,
            MOVEMENTS = 1,
        }
    }
}
