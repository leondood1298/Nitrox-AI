using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic;

public static class BasePowerSources
{
    public static BasePowerSourceType GetSourceType(Component component)
    {
        if (component is SolarPanel || component.GetComponentInParent<SolarPanel>())
        {
            return BasePowerSourceType.SOLAR;
        }
        if (component is ThermalPlant || component.GetComponentInParent<ThermalPlant>())
        {
            return BasePowerSourceType.THERMAL;
        }
        if (component is BaseBioReactor || component.GetComponentInParent<BaseBioReactor>())
        {
            return BasePowerSourceType.BIOREACTOR;
        }
        if (component is BaseNuclearReactor || component.GetComponentInParent<BaseNuclearReactor>())
        {
            return BasePowerSourceType.NUCLEAR;
        }
        return BasePowerSourceType.UNKNOWN;
    }
}
