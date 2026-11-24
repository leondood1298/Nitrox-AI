using System.Collections;
using Nitrox.Model.Core;
using Nitrox.Model.Helper;
using NitroxClient.Helpers;
using NitroxClient.MonoBehaviours.Discord;
using NitroxClient.MonoBehaviours.Gui.MainMenu.ServerJoin;
using NitroxClient.MonoBehaviours.Gui.MainMenu.ServersList;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NitroxClient.MonoBehaviours.Gui.MainMenu;

public class NitroxMainMenuModifications : MonoBehaviour
{
    private const string HOME_GROUP = "Home";
    private static bool startupWarningsShown;
    private MainMenuRightSide rightSide;

    private void OnEnable() => SceneManager.sceneLoaded += SceneManager_sceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= SceneManager_sceneLoaded;

    private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name == "XMenu")
        {
            rightSide = MainMenuRightSide.main;
            MultiplayerMenuMods();
            DiscordClient.InitializeRPMenu();
            if (!startupWarningsShown)
            {
                startupWarningsShown = true;
                StartCoroutine(ShowStartupWarnings());
            }
        }
    }

    /// <summary>
    ///     Tells the player once per run when Nitrox may misbehave: an untested game build or patches that failed.
    /// </summary>
    private static IEnumerator ShowStartupWarnings()
    {
        // The menu opens its home panel during its own start; showing the message before that would be covered at once.
        yield return new WaitForSecondsRealtime(1);

        int? gameBuild = GameBuild.TryRead(Application.dataPath);
        GameBuildSupport support = GameBuild.Classify(gameBuild);
        string message = StartupWarnings.BuildMainMenuMessage(StartupWarnings.FailedPatches, gameBuild, support, NitroxEnvironment.GameNewestTestedVersion,
                                                              Language.main.Get("Nitrox_PatchesFailed"), Language.main.Get("Nitrox_UntestedGameBuild"));
        if (message != null)
        {
            Log.Warn(message);
            MainMenuNotificationPanel.ShowMessage(message, HOME_GROUP);
        }
    }

    private void MultiplayerMenuMods()
    {
        GameObject startButton = GameObjectExtensions.RequireGameObject("Menu canvas/Panel/MainMenu/PrimaryOptions/MenuButtons/ButtonPlay");
        GameObject showLoadedMultiplayer = Instantiate(startButton, startButton.transform.parent);
        showLoadedMultiplayer.name = "ButtonMultiplayer";
        showLoadedMultiplayer.transform.SetSiblingIndex(3);

        TextMeshProUGUI buttonText = showLoadedMultiplayer.RequireGameObject("Circle/Bar/Text").GetComponent<TextMeshProUGUI>();
        buttonText.text = Language.main.Get("Nitrox_Multiplayer");
        buttonText.GetComponent<TranslationLiveUpdate>().translationKey = "Nitrox_Multiplayer";

        Button showLoadedMultiplayerButton = showLoadedMultiplayer.GetComponent<Button>();
        showLoadedMultiplayerButton.onClick = new Button.ButtonClickedEvent();
        showLoadedMultiplayerButton.onClick.AddListener(() => rightSide.OpenGroup(MainMenuServerListPanel.NAME));

        GameObject savedGamesRef = rightSide.gameObject.RequireGameObject("SavedGames");

        GameObject CloneMainMenuLoadPanel(string panelName, string translationKey)
        {
            GameObject menuPanel = Instantiate(savedGamesRef, rightSide.transform);
            menuPanel.name = panelName;
            Transform header = menuPanel.RequireTransform("Header");
            header.GetComponent<TextMeshProUGUI>().text = Language.main.Get(translationKey);
            header.GetComponent<TranslationLiveUpdate>().translationKey = translationKey;
            Destroy(menuPanel.RequireGameObject("Scroll View/Viewport/SavedGameAreaContent/NewGame"));
            Destroy(menuPanel.GetComponent<MainMenuLoadPanel>());
            Destroy(menuPanel.GetComponentInChildren<MainMenuLoadMenu>());

            rightSide.groups.Add(menuPanel.GetComponent<MainMenuGroup>());
            return menuPanel;
        }

        GameObject serverJoinNotification = CloneMainMenuLoadPanel(MainMenuNotificationPanel.NAME, string.Empty);
        serverJoinNotification.AddComponent<MainMenuNotificationPanel>().Setup(savedGamesRef);

        GameObject serverJoin = CloneMainMenuLoadPanel(MainMenuJoinServerPanel.NAME, "Nitrox_JoinServer");
        serverJoin.AddComponent<MainMenuJoinServerPanel>().Setup(savedGamesRef);

        GameObject serverPasswordEnter = CloneMainMenuLoadPanel(MainMenuEnterPasswordPanel.NAME, "Nitrox_JoinServerPasswordHeader");
        serverPasswordEnter.AddComponent<MainMenuEnterPasswordPanel>().Setup(savedGamesRef);

        GameObject serverList = CloneMainMenuLoadPanel(MainMenuServerListPanel.NAME, "Nitrox_Multiplayer");
        serverList.AddComponent<MainMenuServerListPanel>().Setup(savedGamesRef);

        GameObject serverCreate = CloneMainMenuLoadPanel(MainMenuCreateServerPanel.NAME, "Nitrox_AddServer");
        serverCreate.AddComponent<MainMenuCreateServerPanel>().Setup(savedGamesRef);
        // The single-player Play button stays: every patch that changes single-player play acts only during a multiplayer session.
    }
}
