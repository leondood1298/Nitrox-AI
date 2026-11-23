using System;
using FMOD.Studio;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic.FMOD;

namespace NitroxClient.GameLogic;

/// <summary>
///     The part that recharges a camera in a dock.
/// </summary>
public sealed partial class MapRoomCameraStates
{
    /// <summary>
    ///     The game's own number, read from the IL of MapRoomCamera.UpdateEnergyRecharge.
    /// </summary>
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

    /// <summary>
    ///     Whether a docked camera that another player's game recharges is charging now, from what this game holds of
    ///     it: the battery the simulating player last reported, and the power of its base. The game's own rule
    ///     (MapRoomCamera.UpdateEnergyRecharge) is a battery below its capacity and a base that gave it energy.
    /// </summary>
    /// <param name="reportedEnergy">The battery as the simulating player last reported it.</param>
    /// <param name="basePowered">Whether the base has power to give, or the game mode needs none.</param>
    internal static bool IsRechargedByAnotherPlayer(float reportedEnergy, float capacity, bool basePowered) =>
        // The last bit of a charge can stay unreported: a report needs a change of at least ENERGY_REPORT_THRESHOLD.
        basePowered && capacity - reportedEnergy >= ENERGY_REPORT_THRESHOLD;

    private void Recharge(MapRoomCamera camera)
    {
        if (!CanSimulateDockedCamera(camera))
        {
            PlayChargingOfAnotherPlayer(camera);
            ReportIfChanged(camera);
            return;
        }

        bool charging = false;
        if (camera.energyMixin.battery != null)
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
        SetChargingSound(camera, charging);
        ReportIfChanged(camera);
    }

    /// <summary>
    ///     The game plays a camera's charging sound only where the camera is recharged, which is the game of the player
    ///     who simulates it. Everybody else hears it from what the server holds of the camera, without packets, which
    ///     also holds for a player who comes near or joins while it charges.
    /// </summary>
    private void PlayChargingOfAnotherPlayer(MapRoomCamera camera)
    {
        bool charging = false;
        if (camera && camera.dockingPoint && camera.energyMixin.battery != null && camera.TryGetNitroxId(out NitroxId _))
        {
            PowerRelay relay = camera.dockingPoint.GetComponentInParent<PowerRelay>();
            bool basePowered = !GameModeUtils.RequiresPower() || relay && relay.GetPower() > 0f;
            charging = IsRechargedByAnotherPlayer(camera.energyMixin.charge, camera.energyMixin.capacity, basePowered);
        }
        // Each game decides this for itself; a sound sent on from here would reach games that already play it.
        using (FMODSystem.SuppressSendingSounds())
        {
            SetChargingSound(camera, charging);
        }
    }

    private static void SetChargingSound(MapRoomCamera camera, bool charging)
    {
        if (!camera)
        {
            return;
        }
        if (charging)
        {
            camera.chargingSound.Play();
        }
        else
        {
            // As the game stops it (MapRoomCamera.UpdateEnergyRecharge): the loop fades out.
            camera.chargingSound.Stop(STOP_MODE.ALLOWFADEOUT);
        }
    }

    /// <summary>
    ///     A camera in a dock is recharged by the player who simulates it, like everything else about its battery.
    /// </summary>
    private bool CanSimulateDockedCamera(MapRoomCamera camera) =>
        camera && camera.dockingPoint && camera.TryGetNitroxId(out NitroxId cameraId) && simulationOwnership.HasAnyLockType(cameraId);
}
