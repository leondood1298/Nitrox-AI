using System;
using FMOD.Studio;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     The part that recharges a camera in a dock.
/// </summary>
public sealed partial class MapRoomCameraStates
{
    private const float DOCK_CHARGE_FRACTION_PER_UPDATE = 0.1f;

    private readonly BasePowerConsumptionForwarder powerConsumptionForwarder;

    /// <summary>
    ///     Stands in for the game's recharging of a camera (MapRoomCamera.UpdateEnergyRecharge), which every player's
    ///     game would do for every docked camera, each taking the energy from the base.
    /// </summary>
    public void UpdateEnergyRecharge(MapRoomCamera camera)
    {
        // The base power the camera draws is reported to whoever simulates the power sources.
        powerConsumptionForwarder.EnterConsumer();
        try
        {
            Recharge(camera);
        }
        finally
        {
            powerConsumptionForwarder.ExitConsumer();
        }
    }

    /// <summary>
    ///     Energy a docked camera takes in one update. This is the game's own rule (MapRoomCamera.UpdateEnergyRecharge):
    ///     a tenth of the battery's capacity per update until full, so a docked camera is recharged almost at once.
    /// </summary>
    internal static float CalculateDockCharge(float current, float capacity)
    {
        if (capacity <= 0f || current >= capacity)
        {
            return 0f;
        }
        return Math.Min(capacity - Math.Max(0f, current), capacity * DOCK_CHARGE_FRACTION_PER_UPDATE);
    }

    private void Recharge(MapRoomCamera camera)
    {
        bool charging = false;
        if (CanSimulateDockedCamera(camera) && camera.energyMixin.battery != null)
        {
            float current = camera.energyMixin.charge;
            float amount = CalculateDockCharge(current, camera.energyMixin.capacity);
            if (amount > 0f)
            {
                PowerRelay relay = camera.dockingPoint.GetComponentInParent<PowerRelay>();
                float consumed = 0f;
                if (relay)
                {
                    PowerSystem.ConsumeEnergy(relay, amount, out consumed);
                }
                // As in the game, whatever the base supplied is added, even when it could not supply all that was asked.
                // ConsumeEnergy returns false for a partial draw but has still taken that energy from the base.
                if (!GameModeUtils.RequiresPower())
                {
                    camera.energyMixin.AddEnergy(amount);
                    charging = true;
                }
                else if (consumed > 0f)
                {
                    camera.energyMixin.AddEnergy(consumed);
                    charging = true;
                }
            }
        }
        if (charging)
        {
            camera.chargingSound.Play();
        }
        else
        {
            camera.chargingSound.Stop(STOP_MODE.IMMEDIATE);
        }
        ReportIfChanged(camera);
    }

    /// <summary>
    ///     A camera in a dock is recharged by the player who simulates it, like everything else about its battery.
    /// </summary>
    private bool CanSimulateDockedCamera(MapRoomCamera camera) =>
        camera && camera.dockingPoint && camera.TryGetNitroxId(out NitroxId cameraId) && simulationOwnership.HasAnyLockType(cameraId);
}
