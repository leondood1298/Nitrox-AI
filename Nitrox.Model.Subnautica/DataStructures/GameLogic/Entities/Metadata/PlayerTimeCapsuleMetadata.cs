using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The text and the title of a rocket's time capsule (PlayerTimeCapsule). Its picture is a screenshot on the computer
///     of the player who chose it and is not part of it.
/// </summary>
[Serializable]
[DataContract]
public class PlayerTimeCapsuleMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public string Text { get; }

    /// <summary>
    ///     The game has no field for it and submits a generated title; kept so that whatever sets it is shared too.
    /// </summary>
    [DataMember(Order = 2)]
    public string Title { get; }

    [IgnoreConstructor]
    protected PlayerTimeCapsuleMetadata()
    {
        //Constructor for serialization. Has to be "protected" for json serialization.
    }

    public PlayerTimeCapsuleMetadata(string text, string title)
    {
        Text = text;
        Title = title;
    }

    public override string ToString()
    {
        return $"[{nameof(PlayerTimeCapsuleMetadata)} Text: {Text}, Title: {Title}]";
    }
}
