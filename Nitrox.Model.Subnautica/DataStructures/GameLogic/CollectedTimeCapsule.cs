using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic;

/// <summary>
///     A time capsule of another player of the game that was found in the world and collected. Its content comes from the
///     game's online service to the player who found it only, and the encyclopedia shows it from there
///     (TimeCapsuleContentProvider). The picture is an image on that service, named by <see cref="ImageUrl" />.
/// </summary>
[Serializable]
[DataContract]
public class CollectedTimeCapsule
{
    /// <summary>
    ///     The capsule's content id, which is also its encyclopedia key (TimeCapsule.id).
    /// </summary>
    [DataMember(Order = 1)]
    public string Id { get; set; }

    [DataMember(Order = 2)]
    public string Title { get; set; }

    [DataMember(Order = 3)]
    public string Text { get; set; }

    [DataMember(Order = 4)]
    public string ImageUrl { get; set; }

    [IgnoreConstructor]
    protected CollectedTimeCapsule()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public CollectedTimeCapsule(string id, string title, string text, string imageUrl)
    {
        Id = id;
        Title = title;
        Text = text;
        ImageUrl = imageUrl;
    }

    public override string ToString()
    {
        return $"[{nameof(CollectedTimeCapsule)} {nameof(Id)}: {Id}, {nameof(Title)}: {Title}]";
    }
}
