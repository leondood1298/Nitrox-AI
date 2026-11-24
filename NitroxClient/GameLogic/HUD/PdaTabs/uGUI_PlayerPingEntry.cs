using System.Collections;
using System.Collections.Generic;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors;
using NitroxClient.GameLogic.HUD.Components;
using NitroxClient.GameLogic.PlayerLogic.PlayerModel.Abstract;
using NitroxClient.MonoBehaviours.Gui.Modals;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using UnityEngine.UI;
using UWE;

namespace NitroxClient.GameLogic.HUD.PdaTabs;

public class uGUI_PlayerPingEntry : uGUI_PingEntry
{
    private uGUI_PlayerListTab parent;
    private INitroxPlayer player;

    public string PlayerName => player?.PlayerName ?? string.Empty;
    public bool IsLocalPlayer => player is LocalPlayer;
    private bool showPing;

    private bool muted
    {
        get
        {
            if (player is RemotePlayer remotePlayer && remotePlayer.PlayerContext != null)
            {
                return remotePlayer.PlayerContext.IsMuted;
            }
            // By default we don't care about the local state
            return false;
        }
    }

    public GameObject ShowObject;
    public GameObject MuteObject;
    public GameObject KickObject;
    public GameObject TeleportToObject;
    public GameObject TeleportToMeObject;

    public Sprite MutedSprite;
    public Sprite UnmutedSprite;
    public Sprite KickSprite;
    public Sprite TeleportToSprite;
    public Sprite TeleportToMeSprite;

    private MutePlayerProcessor mutePlayerProcessor;
    private PermsChangedProcessor permsChangedProcessor;

    public new void Awake()
    {
        mutePlayerProcessor = NitroxServiceLocator.LocateService<MutePlayerProcessor>();
        permsChangedProcessor = NitroxServiceLocator.LocateService<PermsChangedProcessor>();
        mutePlayerProcessor.OnPlayerMuted += OnPlayerMuted;
        permsChangedProcessor.OnPermissionsChanged += OnPermissionsChanged;
    }

    public void OnDestroy()
    {
        // A removed entry would otherwise still be called, on a destroyed object
        mutePlayerProcessor.OnPlayerMuted -= OnPlayerMuted;
        permsChangedProcessor.OnPermissionsChanged -= OnPermissionsChanged;
    }

    private void OnPlayerMuted(SessionId playerId, bool muted)
    {
        if (player is RemotePlayer remotePlayer && remotePlayer.SessionId == playerId)
        {
            RefreshMuteButton();
        }
    }

    private void OnPermissionsChanged(Perms perms) => RefreshButtonsVisibility();

    public IEnumerator Start()
    {
        // We must one frame so that the UI elements are initialized properly
        yield return null;
        // This action must happen after the yield so that they're correctly placed
        UpdateButtonsPosition();
        // We trigger it at least once so that the localizations are updated with the PlayerName
        OnLanguageChanged();
    }

    public void Initialize(string id, string name, uGUI_PlayerListTab parent)
    {
        this.id = id;
        this.parent = parent;

        gameObject.SetActive(true);
        visibilityIcon.sprite = spriteVisible;
        icon.SetForegroundSprite(SpriteManager.Get(SpriteManager.Group.Tab, "TabInventory"));
        showPing = true;

        UpdateLabel(name);
        OnLanguageChanged();

        CoroutineHost.StartCoroutine(AssignSprites());
    }

    public void OnLanguageChanged()
    {
        GetTooltip(ShowObject).TooltipText = GetLocalizedText(showPing ? "Nitrox_HidePing" : "Nitrox_ShowPing");
        GetTooltip(MuteObject).TooltipText = GetLocalizedText(muted ? "Nitrox_Unmute" : "Nitrox_Mute");
        GetTooltip(KickObject).TooltipText = GetLocalizedText("Nitrox_Kick");
        GetTooltip(TeleportToObject).TooltipText = GetLocalizedText("Nitrox_TeleportTo");
        GetTooltip(TeleportToMeObject).TooltipText = GetLocalizedText("Nitrox_TeleportToMe");
    }

    public new void Uninitialize()
    {
        base.Uninitialize();
        player = null;
    }

    public void UpdateLabel(string text)
    {
        label.text = text;
    }

    public void UpdateEntryForNewPlayer(INitroxPlayer newPlayer, LocalPlayer localPlayer, IPacketSender packetSender)
    {
        player = newPlayer;

        UpdateLabel(player.PlayerName);
        Color playerColor = player.PlayerSettings.PlayerColor.ToUnity();
        icon.SetColors(playerColor, playerColor, playerColor);
        RefreshMuteButton();
        RefreshShowButton();

        // We need to update each button's listener whether or not they have enough perms because they may become OP during playtime
        ClearButtonListeners();

        GetToggle(ShowObject).onValueChanged.AddListener(delegate (bool toggled)
        {
            if (player is RemotePlayer remotePlayer)
            {
                PingInstance pingInstance = remotePlayer.PlayerModel.GetComponentInChildren<PingInstance>();
                if (pingInstance)
                {
                    pingInstance.SetVisible(toggled);
                }
                showPing = toggled;
                GetTooltip(ShowObject).TooltipText = GetLocalizedText(toggled ? "Nitrox_HidePing" : "Nitrox_ShowPing");
                visibilityIcon.sprite = toggled ? spriteVisible : spriteHidden;
            }
        });
        // Each of those clicks involves a confirmation modal
        GetToggle(MuteObject).onValueChanged.AddListener(delegate (bool _)
        {
            // The toggle only asks; it shows the player's mute state as the server last said it, whatever the answer
            bool mute = !muted;
            RefreshMuteButton();
            Modal.Get<ConfirmModal>()?.Show(GetLocalizedText(mute ? "Nitrox_Mute" : "Nitrox_Unmute", true), () =>
            {
                if (player is RemotePlayer remotePlayer)
                {
                    packetSender.Send(new ServerCommand($"{(mute ? "" : "un")}mute {player.PlayerName}"));
                }
            });
        });
        GetToggle(KickObject).onValueChanged.AddListener(delegate (bool toggled)
        {
            Modal.Get<ConfirmModal>()?.Show(GetLocalizedText("Nitrox_Kick", true), () =>
            {
                packetSender.Send(new ServerCommand($"kick {player.PlayerName}"));
            });
        });
        GetToggle(TeleportToObject).onValueChanged.AddListener(delegate (bool toggled)
        {
            Modal.Get<ConfirmModal>()?.Show(GetLocalizedText("Nitrox_TeleportTo", true), () =>
            {
                packetSender.Send(new ServerCommand($"warp {player.PlayerName}"));
            });
        });
        GetToggle(TeleportToMeObject).onValueChanged.AddListener(delegate (bool toggled)
        {
            Modal.Get<ConfirmModal>()?.Show(GetLocalizedText("Nitrox_TeleportToMe", true), () =>
            {
                packetSender.Send(new ServerCommand($"warp {player.PlayerName} {localPlayer.PlayerName}"));
            });
        });

        RefreshButtonsVisibility();
    }

    private string GetLocalizedText(string key, bool isQuestion = false)
    {
        return Language.main.Get(isQuestion ? $"{key}Question" : key).Replace("{PLAYER}", PlayerName);
    }

    public void UpdateButtonsPosition()
    {
        float OFFSET = 0f;
        List<GameObject> buttonsToAlign = new() { MuteObject, KickObject, TeleportToObject, TeleportToMeObject };
        foreach (GameObject buttonObject in buttonsToAlign)
        {
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, 0f);
            buttonRect.localPosition = new Vector2(OFFSET, 0);
            OFFSET += 80f;
        }
    }

    /// <summary>
    ///     The player's signal may be hidden by this player's saved preference: the entry starts from that.
    /// </summary>
    private void RefreshShowButton()
    {
        bool visible = true;
        if (player is RemotePlayer remotePlayer)
        {
            PingInstance pingInstance = remotePlayer.PlayerModel ? remotePlayer.PlayerModel.GetComponentInChildren<PingInstance>() : null;
            if (pingInstance)
            {
                visible = pingInstance.visible;
            }
            // The signal is attached a little after the player appears
            else if (NitroxServiceLocator.LocateService<PingPreferences>().TryGet(remotePlayer.PlayerContext.PlayerNitroxId.ToString(), out PingInstancePreference preference))
            {
                visible = preference.Visible;
            }
        }
        showPing = visible;
        GetToggle(ShowObject).SetIsOnWithoutNotify(visible);
        GetTooltip(ShowObject).TooltipText = GetLocalizedText(visible ? "Nitrox_HidePing" : "Nitrox_ShowPing");
        visibilityIcon.sprite = visible ? spriteVisible : spriteHidden;
    }

    private void ClearButtonListeners()
    {
        GetToggle(ShowObject).onValueChanged = new Toggle.ToggleEvent();
        GetToggle(MuteObject).onValueChanged = new Toggle.ToggleEvent();
        GetToggle(KickObject).onValueChanged = new Toggle.ToggleEvent();
        GetToggle(TeleportToObject).onValueChanged = new Toggle.ToggleEvent();
        GetToggle(TeleportToMeObject).onValueChanged = new Toggle.ToggleEvent();
    }

    private IEnumerator AssignSprites()
    {
        yield return new WaitUntil(() => parent.FinishedLoadingAssets);

        // NB: Those textures MUST be exported with a Texture Type of "Sprite (2D and UI)", else they will look blurry not matter what
        // NB 2: Those textures for the buttons are scaled 68x61 but the image inside but not hit the borders to have a better render
        MutedSprite = parent.GetSprite("muted@3x");
        UnmutedSprite = parent.GetSprite("unmuted@3x");
        KickSprite = parent.GetSprite("kick@3x");
        TeleportToSprite = parent.GetSprite("teleport_to@3x");
        TeleportToMeSprite = parent.GetSprite("teleport_to_me@3x");

        MuteObject.FindChild("Eye").GetComponent<Image>().sprite = muted ? MutedSprite : UnmutedSprite;
        KickObject.FindChild("Eye").GetComponent<Image>().sprite = KickSprite;
        TeleportToObject.FindChild("Eye").GetComponent<Image>().sprite = TeleportToSprite;
        TeleportToMeObject.FindChild("Eye").GetComponent<Image>().sprite = TeleportToMeSprite;
    }

    private void RefreshMuteButton()
    {
        GetToggle(MuteObject).SetIsOnWithoutNotify(muted);
        GetTooltip(MuteObject).TooltipText = GetLocalizedText(muted ? "Nitrox_Unmute" : "Nitrox_Mute");
        MuteObject.FindChild("Eye").GetComponent<Image>().sprite = muted ? MutedSprite : UnmutedSprite;
    }

    private void RefreshButtonsVisibility()
    {
        LocalPlayer localPlayer = NitroxServiceLocator.LocateService<LocalPlayer>();

        bool isNotLocalPlayer = !IsLocalPlayer;
        // We don't want any control buttons to appear for the local player
        ShowObject.SetActive(isNotLocalPlayer);

        // The perms here should be the same as the perm each command asks for
        MuteObject.SetActive(isNotLocalPlayer && localPlayer.Permissions >= Perms.MODERATOR);
        KickObject.SetActive(isNotLocalPlayer && localPlayer.Permissions >= Perms.MODERATOR);
        TeleportToObject.SetActive(isNotLocalPlayer && localPlayer.Permissions >= Perms.MODERATOR);
        TeleportToMeObject.SetActive(isNotLocalPlayer && localPlayer.Permissions >= Perms.MODERATOR);
    }

    private Toggle GetToggle(GameObject gameObject)
    {
        return gameObject.GetComponent<Toggle>();
    }

    private ButtonTooltip GetTooltip(GameObject gameObject)
    {
        return gameObject.GetComponent<ButtonTooltip>();
    }
}
