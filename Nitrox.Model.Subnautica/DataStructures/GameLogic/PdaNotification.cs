using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic;

/// <summary>
///     The groups of the game's <c>NotificationManager</c> whose unread markers are kept for each player. The values are
///     the game's.
/// </summary>
public enum PdaNotificationGroup
{
    BLUEPRINTS = 2,
    BUILDER = 3,
    CRAFT_TREE = 4,
    LOG = 5,
    ENCYCLOPEDIA = 7
}

/// <summary>
///     An unread marker in the PDA, keyed as the game's <c>NotificationManager</c> keys it: a blueprint by its tech type's
///     number, a log or encyclopedia entry by the entry's key.
/// </summary>
[Serializable]
[DataContract]
public class PdaNotification : IEquatable<PdaNotification>
{
    [DataMember(Order = 1)]
    public PdaNotificationGroup Group { get; }

    [DataMember(Order = 2)]
    public string Key { get; }

    [IgnoreConstructor]
    protected PdaNotification()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public PdaNotification(PdaNotificationGroup group, string key)
    {
        Group = group;
        Key = key;
    }

    /// <summary>
    ///     Whether the game's group with this number is one whose markers are kept.
    /// </summary>
    public static bool IsKept(int group)
    {
        return Enum.IsDefined(typeof(PdaNotificationGroup), group);
    }

    /// <summary>
    ///     The markers the game raises when it learns a blueprint (KnownTech.UnreadAdd). The game itself only raises the
    ///     builder and craft tree ones for tech that can be built or crafted; it drops the others when it restores them.
    /// </summary>
    public static PdaNotification[] ForBlueprint(string techTypeKey)
    {
        return
        [
            new(PdaNotificationGroup.BLUEPRINTS, techTypeKey),
            new(PdaNotificationGroup.CRAFT_TREE, techTypeKey),
            new(PdaNotificationGroup.BUILDER, techTypeKey)
        ];
    }

    public bool Equals(PdaNotification other)
    {
        if (other is null)
        {
            return false;
        }
        return Group == other.Group && string.Equals(Key, other.Key, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as PdaNotification);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Group * 397) ^ (Key?.GetHashCode() ?? 0);
        }
    }

    public override string ToString()
    {
        return $"[{nameof(PdaNotification)} {Group}: {Key}]";
    }
}
