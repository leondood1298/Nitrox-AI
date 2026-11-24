using NitroxClient.MonoBehaviours.Gui.Input;
using NitroxClient.MonoBehaviours.Gui.Input.KeyBindings.Actions;

namespace Nitrox.Test.Client.MonoBehaviours.Gui.Input;

[TestClass]
public sealed class KeyBindingManagerTest
{
    [TestMethod]
    public void TheDiscordFocusBindingIsTheButtonItWasRegisteredAs()
    {
        KeyBindingManager.GetButton<DiscordFocusBindingAction>().Should().Be((GameInput.Button)(KeyBindingManager.NITROX_BASE_ID + 1));
        KeyBindingManager.GetButton<ChatKeyBindingAction>().Should().Be((GameInput.Button)KeyBindingManager.NITROX_BASE_ID);
    }
}
