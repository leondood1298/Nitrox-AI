using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     What the game's online service answered for a time capsule lying in the world: whether a capsule is there and,
///     if so, what it holds. Every game asks with its own account and can get another answer; the first answer is kept
///     for everybody.
/// </summary>
[Serializable]
[DataContract]
public class TimeCapsuleMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool HasCapsule { get; }

    /// <summary>
    ///     The capsule's instance at the online service, which the game reports when it is opened.
    /// </summary>
    [DataMember(Order = 2)]
    public string InstanceId { get; }

    /// <summary>
    ///     The content's id, under which the game keeps the content and the encyclopedia entry.
    /// </summary>
    [DataMember(Order = 3)]
    public string ContentId { get; }

    [DataMember(Order = 4)]
    public string Title { get; }

    [DataMember(Order = 5)]
    public string Text { get; }

    [DataMember(Order = 6)]
    public string ImageUrl { get; }

    [DataMember(Order = 7)]
    public List<Item> Items { get; }

    [IgnoreConstructor]
    protected TimeCapsuleMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public TimeCapsuleMetadata(bool hasCapsule, string instanceId, string contentId, string title, string text, string imageUrl, List<Item> items)
    {
        HasCapsule = hasCapsule;
        InstanceId = instanceId;
        ContentId = contentId;
        Title = title;
        Text = text;
        ImageUrl = imageUrl;
        Items = items;
    }

    /// <summary>
    ///     A spot where the online service placed no capsule.
    /// </summary>
    public static TimeCapsuleMetadata Empty() => new(false, "", "", "", "", "", new List<Item>());

    public override string ToString()
    {
        return $"[{nameof(TimeCapsuleMetadata)} HasCapsule: {HasCapsule}, InstanceId: {InstanceId}, ContentId: {ContentId}, Title: {Title}, Items: {Items?.Count ?? 0}]";
    }

    /// <summary>
    ///     An item the capsule gives to the player who opens it.
    /// </summary>
    [Serializable]
    [DataContract]
    public class Item
    {
        [DataMember(Order = 1)]
        public NitroxTechType TechType { get; }

        [DataMember(Order = 2)]
        public bool HasBattery { get; }

        [DataMember(Order = 3)]
        public NitroxTechType BatteryType { get; }

        [DataMember(Order = 4)]
        public float BatteryCharge { get; }

        [IgnoreConstructor]
        protected Item()
        {
            // Constructor for serialization. Has to be "protected" for json serialization.
        }

        public Item(NitroxTechType techType, bool hasBattery, NitroxTechType batteryType, float batteryCharge)
        {
            TechType = techType;
            HasBattery = hasBattery;
            BatteryType = batteryType;
            BatteryCharge = batteryCharge;
        }

        public override string ToString()
        {
            return $"[{nameof(Item)} TechType: {TechType}, HasBattery: {HasBattery}, BatteryType: {BatteryType}, BatteryCharge: {BatteryCharge}]";
        }
    }
}
