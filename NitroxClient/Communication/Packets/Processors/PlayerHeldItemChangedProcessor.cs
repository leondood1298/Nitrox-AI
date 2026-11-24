using System;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PlayerHeldItemChangedProcessor : IClientPacketProcessor<PlayerHeldItemChanged>
{
    private readonly PlayerManager playerManager;
    private int defaultLayer;
    private int viewModelLayer;
    private Optional<NitroxId> previousHeldItemId = Optional.Empty;

    public delegate void HeldItemChanged(Optional<NitroxId> previousId);
    public static event HeldItemChanged? OnHeldItemChanged;

    public PlayerHeldItemChangedProcessor(PlayerManager playerManager)
    {
        this.playerManager = playerManager;

        if (NitroxEnvironment.IsNormal)
        {
            SetupLayers();
        }
    }

    public Task Process(ClientProcessorContext context, PlayerHeldItemChanged packet)
    {
        LateProcess(packet, 0);
        return Task.CompletedTask;
    }

    private void LateProcess(PlayerHeldItemChanged packet, int count)
    {
        void Reschedule()
        {
            if (count >= 10)
            {
                Log.Warn($"[{nameof(PlayerHeldItemChangedProcessor)}] Could not find entity with id: {packet.ItemId}.");
                return;
            }

            // The item entity may have just been spawned by another client and may not exist for us.
            // Wait up to 10 frames for it to spawn, and only then conclude that it doesn't exist.
            InvokeHelper.InvokeNextFrame(() => LateProcess(packet, count + 1));
        }

        if (!NitroxEntity.TryGetObjectFrom(packet.ItemId, out GameObject item))
        {
            Reschedule();
            return;
        }

        Pickupable pickupable = item.GetComponent<Pickupable>();
        if (!pickupable)
        {
            Reschedule();
            return;
        }
        // An item just spawned may not be in the remote inventory yet; one held without an inventory never will be,
        // so after the retries it is shown all the same
        if (pickupable.inventoryItem == null && count < 10)
        {
            InvokeHelper.InvokeNextFrame(() => LateProcess(packet, count + 1));
            return;
        }

        if (!playerManager.TryFind(packet.SessionId, out RemotePlayer player))
        {
            Log.Warn($"[{nameof(PlayerHeldItemChangedProcessor)}] Could not find player with session id: {packet.SessionId}.");
            return;
        }

        ItemsContainer inventory = player.Inventory;
        PlayerTool tool = item.GetComponent<PlayerTool>();

        OnHeldItemChanged?.Invoke(previousHeldItemId);
        previousHeldItemId = Optional.Of(packet.ItemId);

        // Copied from QuickSlots
        switch (packet.Type)
        {
            case PlayerHeldItemChanged.ChangeType.DRAW_AS_TOOL:
                Validate.IsTrue(tool);
                ModelPlug.PlugIntoSocket(tool, player.ItemAttachPoint);
                Utils.SetLayerRecursively(item, viewModelLayer);
                foreach (Animator componentsInChild in tool.GetComponentsInChildren<Animator>())
                {
                    componentsInChild.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                if (tool.mainCollider)
                {
                    tool.mainCollider.enabled = false;
                }
                tool.GetComponent<Rigidbody>().isKinematic = true;
                if (tool.TryGetComponent(out Floater floater))
                {
                    floater.collider.enabled = false;
                }
                item.SetActive(true);
                tool.SetHandIKTargetsEnabled(true);
                SafeAnimator.SetBool(player.ArmsController.GetComponent<Animator>(), $"holding_{tool.animToolName}", true);
                player.AnimationController["using_tool_first"] = packet.IsFirstTime != null;
                player.SetHeldTool(tool);

                if (item.TryGetComponent(out FPModel fpModelDraw)) //FPModel needs to be updated
                {
                    fpModelDraw.OnEquip(null, null);
                }
                break;

            case PlayerHeldItemChanged.ChangeType.HOLSTER_AS_TOOL:
                Validate.IsTrue(tool);
                item.SetActive(false);
                Utils.SetLayerRecursively(item, defaultLayer);
                if (tool.mainCollider)
                {
                    tool.mainCollider.enabled = true;
                }
                tool.GetComponent<Rigidbody>().isKinematic = false;
                if (tool.TryGetComponent(out floater))
                {
                    floater.collider.enabled = true;
                }
                pickupable.Reparent(inventory.tr);
                foreach (Animator componentsInChild in tool.GetComponentsInChildren<Animator>())
                {
                    componentsInChild.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }
                SafeAnimator.SetBool(player.ArmsController.GetComponent<Animator>(), $"holding_{tool.animToolName}", false);
                player.AnimationController["using_tool_first"] = false;
                if (player.HeldTool == tool)
                {
                    player.SetHeldTool(null);
                }

                if (item.TryGetComponent(out FPModel fpModelHolster)) //FPModel needs to be updated
                {
                    fpModelHolster.OnUnequip(null, null);
                }

                break;

            // inventoryItem.item is the pickupable itself; an item held without being in an inventory (the Mobile
            // Vehicle Bay) has no inventoryItem (upstream pull request 2891)
            case PlayerHeldItemChanged.ChangeType.DRAW_AS_ITEM:
                pickupable.Reparent(player.ItemAttachPoint);
                pickupable.SetVisible(true);
                Utils.SetLayerRecursively(item, viewModelLayer);
                break;

            case PlayerHeldItemChanged.ChangeType.HOLSTER_AS_ITEM:
                pickupable.Reparent(inventory.tr);
                pickupable.SetVisible(false);
                Utils.SetLayerRecursively(item, defaultLayer);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(packet.Type));
        }
    }

    private void SetupLayers()
    {
        defaultLayer = LayerMask.NameToLayer("Default");
        viewModelLayer = LayerMask.NameToLayer("Viewmodel");
    }
}
