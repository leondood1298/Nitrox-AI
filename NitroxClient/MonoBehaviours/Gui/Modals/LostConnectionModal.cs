namespace NitroxClient.MonoBehaviours.Gui.Modals;

/// <summary>
///     Extends the IngameMenu with a disconnect popup.
/// </summary>
public class LostConnectionModal : Modal
{
    public LostConnectionModal() : base(yesButtonText: "Nitrox_OK", modalText: "Nitrox_LostConnection", freezeGame: true, transparency: 1.0f)
    {
    }

    public override void ClickYes()
    {
        IngameMenu.main.QuitGame(false);
    }
}
