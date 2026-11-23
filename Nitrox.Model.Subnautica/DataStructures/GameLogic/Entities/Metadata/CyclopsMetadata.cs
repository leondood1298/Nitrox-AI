using System.Collections.Generic;
using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class CyclopsMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool SilentRunningOn { get; set; }

    [DataMember(Order = 2)]
    public bool ShieldOn { get; set; }

    [DataMember(Order = 3)]
    public bool SonarOn { get; set; }

    [DataMember(Order = 4)]
    public bool EngineOn { get; set; }

    [DataMember(Order = 5)]
    public int EngineMode { get; set; }

    [DataMember(Order = 6)]
    public float Health { get; set; }

    [DataMember(Order = 7)]
    public bool IsDestroyed { get; set; }

    /// <summary>
    ///     Indexes of the active hull leaks, as decided by the player simulating the Cyclops. Null when unknown (saves
    ///     from before this was stored).
    /// </summary>
    [DataMember(Order = 8)]
    public List<int> DamagePointIndexes { get; set; }

    /// <summary>
    ///     The fires burning inside, as decided by the player simulating the Cyclops. Null when unknown.
    /// </summary>
    [DataMember(Order = 9)]
    public List<CyclopsFireData> Fires { get; set; }

    [IgnoreConstructor]
    protected CyclopsMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public CyclopsMetadata(bool silentRunningOn, bool shieldOn, bool sonarOn, bool engineOn, int engineMode, float health, bool isDestroyed,
        List<int> damagePointIndexes = null, List<CyclopsFireData> fires = null)
    {
        DamagePointIndexes = damagePointIndexes;
        Fires = fires;
        SilentRunningOn = silentRunningOn;
        ShieldOn = shieldOn;
        SonarOn = sonarOn;
        EngineOn = engineOn;
        EngineMode = engineMode;
        Health = health;
        IsDestroyed = isDestroyed;
    }

    public override string ToString()
    {
        return $"[CyclopsMetadata SilentRunningOn: {SilentRunningOn}, ShieldOn: {ShieldOn}, SonarOn: {SonarOn}, EngineOn: {EngineOn}, EngineMode: {EngineMode}, Health: {Health}, IsDestroyed: {IsDestroyed}, Leaks: {DamagePointIndexes?.Count.ToString() ?? "unknown"}, Fires: {Fires?.Count.ToString() ?? "unknown"}]";
    }
}
