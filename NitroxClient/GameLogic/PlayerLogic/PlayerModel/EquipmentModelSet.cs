using System.Collections.Generic;

namespace NitroxClient.GameLogic.PlayerLogic.PlayerModel;

/// <summary>
///     Which worn-equipment models of a player are shown, decided the way <c>Player.EquipmentChanged</c> and
///     <c>Player.HideEquipmentSlot</c> decide it from the game's own table (<c>Player.equipmentModels</c>): per slot, the
///     models of the item in the slot are shown, and the slot's default model when none of them is.
/// </summary>
/// <typeparam name="T">The model, a GameObject in the game.</typeparam>
public sealed class EquipmentModelSet<T> where T : class
{
    private readonly List<Slot> slots = [];

    public void AddSlot(string slotName, IReadOnlyList<KeyValuePair<TechType, T?>> models, T? defaultModel)
    {
        slots.Add(new Slot(slotName, models, defaultModel));
    }

    /// <param name="equipped">The item type in each slot that holds one.</param>
    /// <param name="hiddenSlots">Slots whose models are hidden for now, as the game does for the gloves while the infection is revealed.</param>
    /// <returns>Every model with whether it is shown.</returns>
    public List<KeyValuePair<T, bool>> GetVisibility(IReadOnlyDictionary<string, TechType> equipped, ICollection<string> hiddenSlots)
    {
        List<KeyValuePair<T, bool>> visibility = [];
        foreach (Slot slot in slots)
        {
            bool hidden = hiddenSlots.Contains(slot.Name);
            TechType inSlot = equipped.TryGetValue(slot.Name, out TechType techType) ? techType : TechType.None;
            bool anyShown = false;
            foreach (KeyValuePair<TechType, T?> model in slot.Models)
            {
                bool shown = !hidden && model.Key == inSlot;
                anyShown |= shown;
                if (model.Value != null)
                {
                    visibility.Add(new KeyValuePair<T, bool>(model.Value, shown));
                }
            }
            if (slot.DefaultModel != null)
            {
                visibility.Add(new KeyValuePair<T, bool>(slot.DefaultModel, !anyShown));
            }
        }
        return visibility;
    }

    private sealed record Slot(string Name, IReadOnlyList<KeyValuePair<TechType, T?>> Models, T? DefaultModel);
}
