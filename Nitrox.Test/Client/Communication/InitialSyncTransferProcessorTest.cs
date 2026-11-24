using NitroxClient.Communication.Packets.Processors;

namespace Nitrox.Test.Client.Communication;

[TestClass]
public sealed class InitialSyncTransferProcessorTest
{
    [TestMethod]
    public void TheProgressIsWhatArrivedSinceTheAnnouncementOfTheWhole()
    {
        InitialSyncTransferProcessor.Fraction(1000, 1000, 4000).Should().Be(0f);
        InitialSyncTransferProcessor.Fraction(1000, 3000, 4000).Should().Be(0.5f);
        InitialSyncTransferProcessor.Fraction(1000, 9000, 4000).Should().Be(1f, "headers make the count run a little ahead");
        InitialSyncTransferProcessor.Fraction(1000, 1000, 0).Should().Be(1f);
    }
}
