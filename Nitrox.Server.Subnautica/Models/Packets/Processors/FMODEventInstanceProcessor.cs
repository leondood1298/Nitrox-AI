using Nitrox.Model.GameLogic.FMOD;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class FMODEventInstanceProcessor(PlayerManager playerManager, FmodService fmodService, SoundLoops soundLoops, ILogger<FMODEventInstanceProcessor> logger) : IAuthPacketProcessor<FMODEventInstancePacket>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly FmodService fmodService = fmodService;
    private readonly SoundLoops soundLoops = soundLoops;
    private readonly ILogger<FMODEventInstanceProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, FMODEventInstancePacket packet)
    {
        if (!fmodService.TryGetSoundData(packet.AssetPath, out SoundData soundData))
        {
            logger.ZLogError($"Whitelist has no item for {packet.AssetPath}.");
            return;
        }

        if (!packet.Play)
        {
            // Everyone who might have got the start gets the stop, even if they walked away since
            soundLoops.Stopped(packet.Id, packet.AssetPath);
            await context.SendToOthersAsync(packet);
            return;
        }

        if (packet.Looping)
        {
            soundLoops.Started(context.Sender.SessionId, packet.Id, packet.AssetPath, packet.Volume,
                               new FMODEventInstancePacket(packet.Id, false, packet.AssetPath, packet.Position, packet.Volume, true));
        }
        foreach (Player player in playerManager.GetConnectedPlayers())
        {
            // A loop reaches everyone: the receiving game fades it by distance while it plays, and someone out of range now may come closer
            if (player != context.Sender && (packet.Looping || SoundRange.Reaches(soundData, packet.Position, context.Sender.SubRootId, player.Position, player.SubRootId)))
            {
                await context.SendAsync(packet, player.SessionId);
            }
        }
    }
}
