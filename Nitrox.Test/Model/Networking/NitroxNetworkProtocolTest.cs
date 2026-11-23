namespace Nitrox.Model.Networking;

[TestClass]
public sealed class NitroxNetworkProtocolTest
{
    [TestMethod]
    public void CurrentConnectionKeyIsCompatible()
    {
        Assert.IsTrue(NitroxNetworkProtocol.IsCompatible(NitroxNetworkProtocol.ConnectionKey));
    }

    [TestMethod]
    [DataRow("nitrox")]
    [DataRow("nitrox-ai/0")]
    [DataRow("nitrox-ai/1")]
    [DataRow("nitrox-ai/2")]
    [DataRow("nitrox-ai/3")]
    [DataRow("")]
    [DataRow(null)]
    public void OtherConnectionKeysAreIncompatible(string? connectionKey)
    {
        Assert.IsFalse(NitroxNetworkProtocol.IsCompatible(connectionKey));
    }

    [TestMethod]
    public void NeighbouringEpochsAreIncompatible()
    {
        Assert.IsFalse(NitroxNetworkProtocol.IsCompatible($"nitrox-ai/{NitroxNetworkProtocol.Epoch - 1}"));
        Assert.IsFalse(NitroxNetworkProtocol.IsCompatible($"nitrox-ai/{NitroxNetworkProtocol.Epoch + 1}"));
    }

    [TestMethod]
    [DataRow("nitrox-ai/0", 0)]
    [DataRow("nitrox-ai/1", 1)]
    [DataRow("nitrox-ai/2", 2)]
    public void ProtocolEpochCanBeRead(string connectionKey, int expectedEpoch)
    {
        Assert.IsTrue(NitroxNetworkProtocol.TryGetEpoch(connectionKey, out int epoch));
        Assert.AreEqual(expectedEpoch, epoch);
    }

    [TestMethod]
    [DataRow("nitrox")]
    [DataRow("nitrox-ai/")]
    [DataRow("nitrox-ai/-1")]
    [DataRow("nitrox-ai/not-a-number")]
    [DataRow(null)]
    public void InvalidProtocolEpochCannotBeRead(string? connectionKey)
    {
        Assert.IsFalse(NitroxNetworkProtocol.TryGetEpoch(connectionKey, out _));
    }
}
