using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The rocket only launches with every player in the world aboard, so that nobody is left behind while the others
///     watch the credits and leave. Nothing is stored: as in single player, the world after the credits is the world
///     before the launch.
/// </summary>
internal sealed class RocketLaunchManager(PlayerManager playerManager, EntityRegistry entityRegistry, IPacketSender packetSender, ILogger<RocketLaunchManager> logger)
{
    private const string ROCKET_TECH_TYPE = "RocketBase";

    private readonly PlayerManager playerManager = playerManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly IPacketSender packetSender = packetSender;
    private readonly ILogger<RocketLaunchManager> logger = logger;

    public async Task RequestLaunchAsync(Player requester, RocketLaunchRequest request)
    {
        List<string> playersNotAboard = GetPlayersNotAboard(request.RocketId, request.CockpitCenter);
        if (playersNotAboard.Count > 0)
        {
            logger.ZLogInformation($"{requester.Name} tried to launch the rocket {request.RocketId}, but these players are not aboard: {string.Join(", ", playersNotAboard)}");
            await packetSender.SendPacketAsync(new RocketLaunchRefused(request.RocketId, playersNotAboard), requester.SessionId);
            return;
        }

        logger.ZLogInformation($"{requester.Name} launched the rocket {request.RocketId} with every player aboard");
        // The requester too: its game waits for this answer before it starts the ending.
        await packetSender.SendPacketToAllAsync(new RocketLaunch(request.RocketId));
    }

    /// <summary>
    ///     Names of the players in the world who are not in the cockpit. A player still loading the world is not in it
    ///     yet and does not count.
    /// </summary>
    public List<string> GetPlayersNotAboard(NitroxId rocketId, NitroxVector3 cockpitCenter)
    {
        List<Player> playersInWorld = playerManager.ConnectedPlayers().Where(player => player.HasFinishedInitialSync).ToList();
        if (!IsCockpitOf(rocketId, cockpitCenter))
        {
            logger.ZLogWarning($"Rejected a launch of {rocketId}: no rocket the server knows has its cockpit at {cockpitCenter}");
            return playersInWorld.Select(player => player.Name).OrderBy(name => name).ToList();
        }

        // The client also checks that its player is not underwater, but a point this close to the cockpit, which sits on
        // top of the stages above the sea, never is. A player in a base or Cyclops cannot be in the cockpit.
        return playersInWorld.Where(player => player.SubRootId.HasValue || !RocketCockpit.IsAboard(cockpitCenter, player.Position))
                             .Select(player => player.Name)
                             .OrderBy(name => name)
                             .ToList();
    }

    private bool IsCockpitOf(NitroxId rocketId, NitroxVector3 cockpitCenter)
    {
        return entityRegistry.TryGetEntityById(rocketId, out WorldEntity? rocket) &&
               rocket.TechType?.Name == ROCKET_TECH_TYPE &&
               RocketCockpit.CanBeCockpitOf(rocket.Transform.Position, cockpitCenter);
    }
}
