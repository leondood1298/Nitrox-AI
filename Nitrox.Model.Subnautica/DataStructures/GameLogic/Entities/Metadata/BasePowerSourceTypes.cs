namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

public static class BasePowerSourceTypes
{
    private const float SOLAR_MAX_POWER = 75f;
    private const float THERMAL_MAX_POWER = 250f;
    private const float BIOREACTOR_MAX_POWER = 500f;
    public const float NUCLEAR_MAX_POWER = 2500f;
    public const float BIOREACTOR_MAX_FUEL_PROGRESS = 840f;
    public const float NUCLEAR_MAX_FUEL_PROGRESS = 20000f;

    public static bool TryGetMaxPower(BasePowerSourceType sourceType, out float maxPower)
    {
        maxPower = sourceType switch
        {
            BasePowerSourceType.SOLAR => SOLAR_MAX_POWER,
            BasePowerSourceType.THERMAL => THERMAL_MAX_POWER,
            BasePowerSourceType.BIOREACTOR => BIOREACTOR_MAX_POWER,
            BasePowerSourceType.NUCLEAR => NUCLEAR_MAX_POWER,
            _ => 0f
        };
        return maxPower > 0f;
    }

    public static bool TryGetMaxFuelProgress(BasePowerSourceType sourceType, out float maxFuelProgress)
    {
        maxFuelProgress = sourceType switch
        {
            BasePowerSourceType.BIOREACTOR => BIOREACTOR_MAX_FUEL_PROGRESS,
            BasePowerSourceType.NUCLEAR => NUCLEAR_MAX_FUEL_PROGRESS,
            _ => 0f
        };
        return maxFuelProgress > 0f;
    }
}
