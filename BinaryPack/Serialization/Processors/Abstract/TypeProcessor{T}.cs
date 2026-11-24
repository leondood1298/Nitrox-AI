using System.Reflection;
using System.Reflection.Emit;
using BinaryPack.Delegates;
using BinaryPack.Serialization.Reflection;

namespace BinaryPack.Serialization.Processors.Abstract
{
    /// <summary>
    /// A <see langword="class"/> responsible for creating the serializers and deserializers for a given type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The type of items to serialize and deserialize</typeparam>
    internal abstract class TypeProcessor<T>
    {
        private readonly DynamicMethod<BinarySerializer<T>> serializerInfo = DynamicMethod<BinarySerializer<T>>.New();

        /// <summary>
        /// The <see cref="DynamicMethod{T}"/> instance holding the serializer being built for items of type <typeparamref name="T"/>
        /// </summary>
        public DynamicMethod<BinarySerializer<T>> SerializerInfo
        {
            get
            {
                if (!startedEmitting)
                {
                    EmitMethods();
                }

                return serializerInfo;
            }
        }

        private readonly DynamicMethod<BinaryDeserializer<T>> deserializerInfo = DynamicMethod<BinaryDeserializer<T>>.New();

        /// <summary>
        /// The <see cref="DynamicMethod{T}"/> instance holding the deserializer being built for items of type <typeparamref name="T"/>
        /// </summary>
        public DynamicMethod<BinaryDeserializer<T>> DeserializerInfo
        {
            get
            {
                if (!startedEmitting)
                {
                    EmitMethods();
                }

                return deserializerInfo;
            }
        }

        private BinarySerializer<T>? serializer;

        /// <summary>
        /// Gets the <see cref="BinarySerializer{T}"/> instance for items of the current type <typeparamref name="T"/>
        /// </summary>
        public BinarySerializer<T> Serializer
        {
            get
            {
                if (serializer == null)
                {
                    BuildMethods();
                }

                return serializer!;
            }
        }

        private BinaryDeserializer<T>? deserializer;

        /// <summary>
        /// Gets the <see cref="BinaryDeserializer{T}"/> instance for items of the current type <typeparamref name="T"/>
        /// </summary>
        public BinaryDeserializer<T> Deserializer
        {
            get
            {
                if (deserializer == null)
                {
                    BuildMethods();
                }

                return deserializer!;
            }
        }

        private bool startedEmitting;

        private void EmitMethods()
        {
            if (startedEmitting) return;

            startedEmitting = true;

            SerializerInfo.Emit(EmitSerializer);
            if (CountsNesting)
            {
                // The body goes into its own method, so that the depth is counted once around it whatever way it returns
                DynamicMethod<BinaryDeserializer<T>> body = DynamicMethod<BinaryDeserializer<T>>.New();
                body.Emit(EmitDeserializer);
                DeserializerInfo.Emit(il => EmitNestingCounter(il, body.MethodInfo));
            }
            else
            {
                DeserializerInfo.Emit(EmitDeserializer);
            }
        }

        /// <summary>
        /// Whether reading an item of this type counts as one level of nesting. Only objects can lead back to their
        /// own type, so only their processors count, which bounds the recursion of any data.
        /// </summary>
        protected virtual bool CountsNesting => false;

        /// <summary>
        /// reader.EnterNested(); T result = body(ref reader); reader.ExitNested(); return result;
        /// </summary>
        /// <remarks>A throw needs no matching exit: it ends the whole read, reader included.</remarks>
        private static void EmitNestingCounter(ILGenerator il, MethodInfo body)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, KnownMembers.BinaryReader.EnterNested);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, body);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, KnownMembers.BinaryReader.ExitNested);
            il.Emit(OpCodes.Ret);
        }

        private void BuildMethods()
        {
            if (!startedEmitting)
            {
                EmitMethods();
            }

            serializer = SerializerInfo.Build();
            deserializer = DeserializerInfo.Build();
        }

        /// <summary>
        /// Emits the instructions for the dynamic serializer method for items of type <typeparamref name="T"/>
        /// </summary>
        /// <param name="il">The input <see cref="ILGenerator"/> instance to use to emit instructions</param>
        protected abstract void EmitSerializer(ILGenerator il);

        /// <summary>
        /// Emits the instructions for the dynamic deserializer method for items of type <typeparamref name="T"/>
        /// </summary>
        /// <param name="il">The input <see cref="ILGenerator"/> instance to use to emit instructions</param>
        protected abstract void EmitDeserializer(ILGenerator il);
    }
}
