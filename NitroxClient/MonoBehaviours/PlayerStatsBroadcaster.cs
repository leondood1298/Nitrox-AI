using Nitrox.Model.DataStructures;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public class PlayerStatsBroadcaster : MonoBehaviour
{
    private float time;
    private const float BROADCAST_INTERVAL = 3f;
    private const string TANK_SLOT = "Tank";

    /// <summary>
    ///     Below this change an equipped tank's level is not sent again.
    /// </summary>
    private const float TANK_OXYGEN_EPSILON = 0.5f;

    private LocalPlayer localPlayer;
    private Entities entities;
    private Survival survival;
    private NitroxId lastTankId;
    private float lastTankOxygen = float.NaN;

    public void Awake()
    {
        localPlayer = this.Resolve<LocalPlayer>();
        entities = this.Resolve<Entities>();
        survival = Player.main.AliveOrNull()?.GetComponent<Survival>();
        if (!survival)
        {
            Log.Error($"Couldn't find the {nameof(Survival)} instance on the main {nameof(Player)} instance. Destroying {nameof(PlayerStatsBroadcaster)}");
            Destroy(this);
        }
    }

    public void Update()
    {
        time += Time.deltaTime;

        // Only do on a specific cadence to avoid hammering server
        if (time >= BROADCAST_INTERVAL)
        {
            time = 0;

            float oxygen = Player.main.oxygenMgr.GetOxygenAvailable();
            float maxOxygen = Player.main.oxygenMgr.GetOxygenCapacity();
            float health = Player.main.liveMixin.health;
            float food = survival.food;
            float water = survival.water;
            float infectionAmount = Player.main.infectedMixin.GetInfectedAmount();
            localPlayer.BroadcastStats(oxygen, maxOxygen, health, food, water, infectionAmount);
            BroadcastEquippedTankOxygen();
        }
    }

    /// <summary>
    ///     A tank's oxygen level is otherwise only stored when it is picked up or unequipped, so a player who left
    ///     with a tank on their back got it back at whatever level it had then.
    /// </summary>
    private void BroadcastEquippedTankOxygen()
    {
        InventoryItem tank = Inventory.main && Inventory.main.equipment != null ? Inventory.main.equipment.GetItemInSlot(TANK_SLOT) : null;
        if (tank == null || !tank.item || !tank.item.TryGetComponent(out Oxygen tankOxygen) || !tank.item.TryGetNitroxId(out NitroxId tankId))
        {
            return;
        }
        if (tankId.Equals(lastTankId) && Mathf.Abs(tankOxygen.oxygenAvailable - lastTankOxygen) < TANK_OXYGEN_EPSILON)
        {
            return;
        }

        lastTankId = tankId;
        lastTankOxygen = tankOxygen.oxygenAvailable;
        entities.EntityMetadataChanged(tankOxygen, tankId);
    }
}
