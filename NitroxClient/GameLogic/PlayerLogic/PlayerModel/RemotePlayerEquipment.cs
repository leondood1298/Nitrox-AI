using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UWE;

namespace NitroxClient.GameLogic.PlayerLogic.PlayerModel;

/// <summary>
///     The worn equipment shown on another player's body. The models are those of the game's own table
///     (<c>Player.equipmentModels</c> of the local player), found on the body by their place below the player's
///     <c>body</c> object, of which the body is a copy.
/// </summary>
public sealed class RemotePlayerEquipment
{
    private readonly EquipmentModelSet<GameObject> models = new();
    private readonly Dictionary<string, TechType> equipped = [];
    private readonly HashSet<string> hiddenSlots = [];
    private int hideChange;

    public RemotePlayerEquipment(Transform localBody, Transform remoteBody, string playerName)
    {
        Player.EquipmentType[] table = Player.main ? Player.main.equipmentModels : null;
        if (table == null)
        {
            Log.Warn($"[{nameof(RemotePlayerEquipment)}] The local player has no equipment models; {playerName}'s equipment will not be shown");
            return;
        }

        foreach (Player.EquipmentType type in table)
        {
            List<KeyValuePair<TechType, GameObject?>> slotModels = [];
            if (type.equipment != null)
            {
                foreach (Player.EquipmentModel model in type.equipment)
                {
                    slotModels.Add(new KeyValuePair<TechType, GameObject?>(model.techType, FindCopy(model.model, localBody, remoteBody)));
                }
            }
            models.AddSlot(type.slot, slotModels, FindCopy(type.defaultModel, localBody, remoteBody));
        }
        Apply();
    }

    public void SetEquipped(IEnumerable<KeyValuePair<string, TechType>> slotsWithItems)
    {
        equipped.Clear();
        foreach (KeyValuePair<string, TechType> slotWithItem in slotsWithItems)
        {
            equipped[slotWithItem.Key] = slotWithItem.Value;
        }
        Apply();
    }

    /// <summary>
    ///     Shows a slot as empty for a while, as <c>Player.StartHideGlovesFor</c> does for the gloves during the
    ///     infection reveal.
    /// </summary>
    public void HideSlotFor(string slot, float seconds)
    {
        hiddenSlots.Add(slot);
        Apply();
        CoroutineHost.StartCoroutine(ShowSlotAfter(slot, seconds, ++hideChange));
    }

    /// <summary>
    ///     Shows again what is worn at once, as after the player's death.
    /// </summary>
    public void ShowAllSlots()
    {
        hideChange++;
        hiddenSlots.Clear();
        Apply();
    }

    private IEnumerator ShowSlotAfter(string slot, float seconds, int change)
    {
        yield return new WaitForSeconds(seconds);
        if (change == hideChange)
        {
            hiddenSlots.Remove(slot);
            Apply();
        }
    }

    private void Apply()
    {
        foreach (KeyValuePair<GameObject, bool> model in models.GetVisibility(equipped, hiddenSlots))
        {
            // The body may be gone while a hidden slot waits to be shown again
            if (model.Key)
            {
                model.Key.SetActive(model.Value);
            }
        }
    }

    private static GameObject? FindCopy(GameObject? localModel, Transform localBody, Transform remoteBody)
    {
        if (!localModel)
        {
            return null;
        }

        List<string> names = [];
        Transform current = localModel.transform;
        while (current && current != localBody)
        {
            names.Add(current.name);
            current = current.parent;
        }
        if (!current)
        {
            Log.Warn($"[{nameof(RemotePlayerEquipment)}] The equipment model {localModel.name} is not part of the player's body and is not shown on other players");
            return null;
        }

        names.Reverse();
        Transform copy = remoteBody.Find(string.Join("/", names));
        if (!copy)
        {
            Log.Warn($"[{nameof(RemotePlayerEquipment)}] The equipment model {string.Join("/", names)} was not found on another player's body");
            return null;
        }
        return copy.gameObject;
    }
}
