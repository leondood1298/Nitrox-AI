namespace NitroxClient.MonoBehaviours.Gui.Modals;

public class ServerStoppedModal : Modal
{
    public ServerStoppedModal() : base(yesButtonText: "Nitrox_OK", modalText: "Nitrox_ServerStopped", freezeGame: true, transparency: 1.0f)
    {
    }

    public override void ClickYes()
    {
        IngameMenu.main.QuitGame(false);
    }
}
