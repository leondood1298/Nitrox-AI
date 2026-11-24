using System;
using System.Numerics;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.DataStructures.Unity
{
    [DataContract]
    [Serializable]
    public class NitroxTransform
    {
        [DataMember(Order = 1)]
        public NitroxVector3 LocalPosition;

        [DataMember(Order = 2)]
        public NitroxQuaternion LocalRotation;

        [DataMember(Order = 3)]
        public NitroxVector3 LocalScale;

        public Matrix4x4 LocalToWorldMatrix
        {
            get
            {
                Matrix4x4 cachedMatrix = Matrix4x4Extension.Compose(LocalPosition, LocalRotation, LocalScale);

                return Parent != null ? cachedMatrix * Parent.LocalToWorldMatrix : cachedMatrix;
            }
        }

        public NitroxTransform? Parent;

        [IgnoredMember]
        public NitroxVector3 Position
        {
            get
            {
                Matrix4x4 matrix = Parent?.LocalToWorldMatrix ?? Matrix4x4.Identity;
                return matrix.Transform(LocalPosition);
            }
            set
            {
                if (Parent == null)
                {
                    LocalPosition = value;
                    return;
                }
                // A parent without size in some direction cannot be inverted, and its inverse is all NaN. Such a place
                // cannot be stored relative to it, and the last one is kept rather than one that is not a number.
                if (!TryGetWorldToParent(out Matrix4x4 worldToParent))
                {
                    return;
                }
                NitroxVector3 localPosition = (NitroxVector3)(Matrix4x4Extension.Compose(value, ToFiniteRotation(LocalRotation), LocalScale) * worldToParent).Translation;
                if (localPosition.IsFinite())
                {
                    LocalPosition = localPosition;
                }
            }
        }

        [IgnoredMember]
        public NitroxQuaternion Rotation
        {
            get
            {
                if (Parent == null)
                {
                    return LocalRotation;
                }
                // Decomposing a parent without size gives no rotation; the stored one is the best there is then.
                if (!Matrix4x4.Decompose(Parent.LocalToWorldMatrix, out Vector3 _, out Quaternion parentRotation, out Vector3 _) || !((NitroxQuaternion)parentRotation).IsFinite())
                {
                    return LocalRotation;
                }
                return (NitroxQuaternion)parentRotation * LocalRotation;
            }
            set
            {
                if (Parent == null)
                {
                    LocalRotation = value;
                    return;
                }
                if (!TryGetWorldToParent(out Matrix4x4 worldToParent))
                {
                    return;
                }
                Matrix4x4 matrix = Matrix4x4Extension.Compose(LocalPosition, value, LocalScale) * worldToParent;
                if (Matrix4x4.Decompose(matrix, out Vector3 _, out Quaternion rotation, out Vector3 _) && ((NitroxQuaternion)rotation).IsFinite())
                {
                    LocalRotation = (NitroxQuaternion)rotation;
                }
            }
        }

        /// <summary>
        ///     Whether this is the transform <c>new NitroxTransform()</c> makes: no position, a rotation of all zeros (no
        ///     rotation at all) and no size. Entities that have no place of their own keep it, a Scanner Room among
        ///     them. It is no place anything could be relative to.
        /// </summary>
        public bool IsEmpty() => LocalPosition == NitroxVector3.Zero && LocalRotation == default && LocalScale == NitroxVector3.Zero;

        /// <param name="parent">
        ///     The transform this one is to be relative to. An empty one (see <see cref="IsEmpty" />) is no place, so a
        ///     child of it stays relative to the world: linked to it, every place of the child would collapse into one
        ///     point that cannot be inverted, and the next place given would turn into NaN. A child of an entity with an
        ///     empty transform, such as a camera docked in a Scanner Room, holds a place in the world while the server
        ///     runs, and so it stays after a load.
        /// </param>
        public void SetParent(NitroxTransform? parent, bool worldPositionStays = true)
        {
            if (parent != null && parent.IsEmpty())
            {
                parent = null;
            }

            if (!worldPositionStays)
            {
                Parent = parent;
                return;
            }

            NitroxVector3 position = Position;
            NitroxQuaternion rotation = Rotation;

            Parent = parent;

            Position = position;
            Rotation = rotation;
        }

        private bool TryGetWorldToParent(out Matrix4x4 worldToParent) => Matrix4x4.Invert(Parent!.LocalToWorldMatrix, out worldToParent) && IsFinite(worldToParent);

        private static bool IsFinite(Matrix4x4 m) =>
            new NitroxQuaternion(m.M11, m.M12, m.M13, m.M14).IsFinite() && new NitroxQuaternion(m.M21, m.M22, m.M23, m.M24).IsFinite() &&
            new NitroxQuaternion(m.M31, m.M32, m.M33, m.M34).IsFinite() && new NitroxQuaternion(m.M41, m.M42, m.M43, m.M44).IsFinite();

        /// <summary>
        ///     A rotation with a NaN in it would turn the position composed with it into NaN as well (0 * NaN is NaN).
        /// </summary>
        private static NitroxQuaternion ToFiniteRotation(NitroxQuaternion rotation) => rotation.IsFinite() ? rotation : NitroxQuaternion.Identity;

        /// <summary>
        ///     False if the stored position, rotation or scale holds a NaN or an infinite number.
        /// </summary>
        public bool IsFinite() => LocalPosition.IsFinite() && LocalRotation.IsFinite() && LocalScale.IsFinite();

        public NitroxTransform() { }

        public NitroxTransform(NitroxVector3 localPosition, NitroxQuaternion localRotation, NitroxVector3 scale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = scale;
        }

        public override string ToString()
        {
            return $"(Position: {Position}, LocalPosition: {LocalPosition}, Rotation: {Rotation}, LocalRotation: {LocalRotation}, LocalScale: {LocalScale})";
        }
    }
}
