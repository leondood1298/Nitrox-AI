using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class FruitPlantMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool[] PickedStates { get; } = [];

    [DataMember(Order = 2)]
    public float TimeNextFruit { get; }

    [IgnoreConstructor]
    protected FruitPlantMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public FruitPlantMetadata(bool[] pickedStates, float timeNextFruit)
    {
        PickedStates = pickedStates;
        TimeNextFruit = timeNextFruit;
    }

    /// <returns>The indices of the fruit that are on the plant (not picked), in the order of the plant's fruit.</returns>
    public static int[] PresentIndices(bool[] pickedStates)
    {
        if (pickedStates == null)
        {
            return [];
        }
        List<int> present = [];
        for (int i = 0; i < pickedStates.Length; i++)
        {
            if (!pickedStates[i])
            {
                present.Add(i);
            }
        }
        return present.ToArray();
    }

    public override string ToString()
    {
        return $"[{nameof(FruitPlantMetadata)} PickedStates: [{string.Join(", ", PickedStates)}], TimeNextFruit: {TimeNextFruit}]";
    }
}
