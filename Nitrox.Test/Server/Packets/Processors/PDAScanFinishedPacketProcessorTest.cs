using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.GameLogic;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class PDAScanFinishedPacketProcessorTest
{
    private static readonly NitroxTechType fragment = new("SeamothFragment");

    private ScannerRoomScenarioFixture scenario;
    private PDAScanFinishedPacketProcessor processor;

    [TestInitialize]
    public void Init()
    {
        scenario = new ScannerRoomScenarioFixture();
        processor = new PDAScanFinishedPacketProcessor(new PdaManager(NullLogger<PdaManager>.Instance), scenario.WorldEntityManager, scenario.Manager);
    }

    [TestMethod]
    public async Task ScansOfTwoPlayersAtOnceAreBothCountedAndSentToEveryone()
    {
        // Both games counted 1 -> 2 locally; the server counts 2 and then 3 and sends each count to all players
        await processor.Process(new AuthProcessorContext(scenario.PlayerA, scenario.PacketSender), new PDAScanFinished(null, fragment, 0, 3, false, false));
        await processor.Process(new AuthProcessorContext(scenario.PlayerB, scenario.PacketSender), new PDAScanFinished(null, fragment, 0, 3, false, false));
        await processor.Process(new AuthProcessorContext(scenario.PlayerB, scenario.PacketSender), new PDAScanFinished(null, fragment, 0, 3, false, false));

        List<PDAScanFinished> sent = ConnectedPlayers.SentToAll<PDAScanFinished>(scenario.PacketSender);
        sent.Select(packet => packet.UnlockedAmount).Should().Equal(1, 2, 3);
        sent.Select(packet => packet.FullyResearched).Should().Equal(false, false, true);
    }
}
