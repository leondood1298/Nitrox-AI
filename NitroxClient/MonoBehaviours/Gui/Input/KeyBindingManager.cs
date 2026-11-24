using System.Collections.Generic;
using NitroxClient.MonoBehaviours.Gui.Input.KeyBindings;
using NitroxClient.MonoBehaviours.Gui.Input.KeyBindings.Actions;

namespace NitroxClient.MonoBehaviours.Gui.Input;

public static class KeyBindingManager
{
    public const int NITROX_BASE_ID = 1000;

    public static List<KeyBinding> KeyBindings =
    [
        new ChatKeyBindingAction(),
        new DiscordFocusBindingAction(),
        new PingKeyBindingAction()
    ];

    /// <summary>
    ///     The game button a Nitrox binding was registered as: they are numbered from <see cref="NITROX_BASE_ID" /> in list order.
    /// </summary>
    public static GameInput.Button GetButton<T>() where T : KeyBinding
    {
        int index = KeyBindings.FindIndex(binding => binding is T);
        return (GameInput.Button)(NITROX_BASE_ID + index);
    }
}
