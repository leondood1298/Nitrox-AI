namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

public enum BasePowerSourceType
{
    UNKNOWN,
    SOLAR,
    THERMAL,
    BIOREACTOR,
    NUCLEAR,
    /// <summary>
    ///     A power cell of the Lifepod (a PowerSource with a RegeneratePowerSource).
    /// </summary>
    ESCAPE_POD
}
