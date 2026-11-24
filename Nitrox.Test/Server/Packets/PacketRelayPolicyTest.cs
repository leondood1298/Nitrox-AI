using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Logger;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Test.Server.Packets;

/// <summary>
///     The server passed every packet without a processor of its own on to the other players, also packets only the server
///     should send (ranks, settings, story events), so a modified game could send them to everyone.
/// </summary>
[TestClass]
public sealed class PacketRelayPolicyTest
{
    [TestMethod]
    public void EveryPacketTypeIsProcessedRelayedOrSentOnlyByTheServer()
    {
        HashSet<Type> processed = PacketRelayPolicy.GetTypesWithServerProcessor();
        Type[] packetTypes = typeof(Packet).Assembly.GetTypes()
                                           .Concat(typeof(SubnauticaInGameLogger).Assembly.GetTypes())
                                           .Where(type => typeof(Packet).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                                           .ToArray();

        string[] unclassified = packetTypes.Where(type => !processed.Contains(type) && !PacketRelayPolicy.RELAYED.Contains(type) && !PacketRelayPolicy.SENT_ONLY_BY_SERVER.Contains(type))
                                           .Select(type => type.Name)
                                           .Order()
                                           .ToArray();

        string.Join(", ", unclassified).Should().BeEmpty("a new packet type must be put in PacketRelayPolicy: relayed between players, or sent only by the server");
    }

    [TestMethod]
    public void NoPacketTypeIsInTwoClasses()
    {
        HashSet<Type> processed = PacketRelayPolicy.GetTypesWithServerProcessor();

        PacketRelayPolicy.RELAYED.Intersect(PacketRelayPolicy.SENT_ONLY_BY_SERVER).Should().BeEmpty();
        PacketRelayPolicy.RELAYED.Intersect(processed).Should().BeEmpty("a packet with a processor is not passed on by the default one");
    }

    [TestMethod]
    public void PacketsOnlyTheServerSendsAreNotRelayed()
    {
        PacketRelayPolicy.IsRelayed(typeof(Nitrox.Model.Subnautica.Packets.PermsChanged)).Should().BeFalse();
        PacketRelayPolicy.IsRelayed(typeof(Nitrox.Model.Subnautica.Packets.KeepInventoryChanged)).Should().BeFalse();
    }
}
