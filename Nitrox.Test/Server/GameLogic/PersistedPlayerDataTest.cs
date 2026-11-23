using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Players;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class PersistedPlayerDataTest
{
    [TestMethod]
    public void SaveKeepsTheUnreadPdaMarkersOfAPlayer()
    {
        ServerPlayer player = new ConnectedPlayers().Connect("Reader");
        PdaNotification logEntry = new(PdaNotificationGroup.LOG, "Story_AuroraWarning1");
        PdaNotification blueprint = new(PdaNotificationGroup.BLUEPRINTS, "757");
        player.UnreadPdaNotifications.Add(logEntry);
        player.UnreadPdaNotifications.Add(blueprint);
        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

        using MemoryStream output = new();
        serializer.Serialize(output, PersistedPlayerData.FromPlayer(player));
        using MemoryStream input = new(output.ToArray());
        ServerPlayer loaded = serializer.Deserialize<PersistedPlayerData>(input).ToPlayer();

        loaded.UnreadPdaNotifications.Should().BeEquivalentTo([logEntry, blueprint]);
    }
}
