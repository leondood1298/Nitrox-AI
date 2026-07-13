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

	public static float GetFuelConsumed(Component component)
	{
		if (component is BaseBioReactor bioReactor || (bool)(bioReactor = component.GetComponentInParent<BaseBioReactor>()))
		{
			return bioReactor._toConsume;
		}
		if (component is BaseNuclearReactor nuclearReactor || (bool)(nuclearReactor = component.GetComponentInParent<BaseNuclearReactor>()))
		{
			return nuclearReactor._toConsume;
		}
		return 0f;
	}

	public static void SetFuelConsumed(Component component, BasePowerSourceType sourceType, float fuelConsumed)
	{
		BasePowerSourceTypes.TryGetMaxFuelProgress(sourceType, out float maximum);
		float accepted = Mathf.Clamp(fuelConsumed, 0f, maximum);
		if (sourceType == BasePowerSourceType.BIOREACTOR && (component is BaseBioReactor bioReactor || (bool)(bioReactor = component.GetComponentInParent<BaseBioReactor>())))
		{
			bioReactor._toConsume = accepted;
		}
		else if (sourceType == BasePowerSourceType.NUCLEAR && (component is BaseNuclearReactor nuclearReactor || (bool)(nuclearReactor = component.GetComponentInParent<BaseNuclearReactor>())))
		{
			nuclearReactor._toConsume = accepted;
		}
	}
}
