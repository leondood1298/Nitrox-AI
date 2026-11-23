using System.Collections.Generic;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic;

public class Rockets
{
    private const string COCKPIT_CENTER_NAME = "AtmosphereVolume";

    private readonly IPacketSender packetSender;
    private readonly PlayerManager playerManager;
    private readonly TimeCapsules timeCapsules;

    /// <summary>
    ///     A launch was asked for and the server has not answered yet. Further clicks would only ask again.
    /// </summary>
    private bool launchRequested;

    public Rockets(IPacketSender packetSender, PlayerManager playerManager, TimeCapsules timeCapsules)
    {
        this.packetSender = packetSender;
        this.playerManager = playerManager;
        this.timeCapsules = timeCapsules;
    }

    /// <summary>
    ///     Asks the server to launch instead of launching: the ending only starts once the server has found every
    ///     player aboard (<see cref="RocketLaunch(NitroxId)" />).
    /// </summary>
    public void RequestRocketLaunch(LaunchRocket launchRocket)
    {
        if (launchRequested)
        {
            return;
        }

        Rocket rocket = launchRocket.RequireComponentInParent<Rocket>();
        if (!rocket.TryGetNitroxEntity(out NitroxEntity entity))
        {
            Log.Error($"{nameof(RequestRocketLaunch)}: Can't find a NitroxEntity attached to the Rocket: {rocket.name}");
            return;
        }

        // The server cannot know where the cockpit is in the rocket, so the client tells it
        GameObject cockpitCenter = rocket.gameObject.FindChild(COCKPIT_CENTER_NAME);
        launchRequested = packetSender.Send(new RocketLaunchRequest(entity.Id, cockpitCenter.transform.position.ToDto()));
    }

    public void RocketLaunchRefused(List<string> playersNotAboard)
    {
        launchRequested = false;
        Log.InGame(Language.main.Get("Nitrox_RocketLaunchPlayersNotAboard").Replace("{PLAYERS}", string.Join(", ", playersNotAboard)));
    }

    public void RocketLaunch(NitroxId rocketId)
    {
        launchRequested = false;

        // Avoid useless calculations
        if (LaunchRocket.launchStarted)
        {
            return;
        }

        // A player who has not loaded the rocket cannot be inside it, and so does not take part in the launch
        if (NitroxEntity.TryGetObjectFrom(rocketId, out GameObject rocketObject))
        {
            RocketLaunch(rocketObject);
        }
    }

    private void RocketLaunch(GameObject rocketObject)
    {
        GameObject sphereCenter = rocketObject.FindChild(COCKPIT_CENTER_NAME);
        LaunchRocket launchRocket = rocketObject.RequireComponentInChildren<LaunchRocket>(true);

        // Only the players in the cockpit take part. The server counted this player aboard, but it may have stepped
        // out since; then it simply stays behind.
        if (Player.main.IsUnderwater() ||
            Player.main.currentSub ||
            !RocketCockpit.IsAboard(sphereCenter.transform.position.ToDto(), Player.main.transform.position.ToDto()))
        {
            return;
        }

        // Code extracted from LaunchRocket::OnHandClick
        LaunchRocket.SetLaunchStarted();
        // With several rockets the game's single capsule may show another rocket's capsule
        timeCapsules.SubmitCapsuleOfRocket(rocketObject);
        launchRocket.StartCoroutine(launchRocket.StartEndCinematic());
        HandReticle.main.RequestCrosshairHide();

        // We also need to hide the other players
        foreach (RemotePlayer player in playerManager.GetAll())
        {
            player.PlayerModel.SetActive(false);
        }

        Log.InGame(Language.main.Get("Nitrox_ThankForPlaying"));
    }
}
