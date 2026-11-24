using Mono.Nat;
using Nitrox.Model.Helper;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Test.Server.Services;

[TestClass]
public sealed class PortForwardServiceTest
{
    [TestMethod]
    public void MappingLeftByAnEarlierNitroxServerIsTakenOver()
    {
        PortForwardService.IsLeftOfNitroxServer("Nitrox server with process id 1234").Should().BeTrue();
    }

    [TestMethod]
    public void ForwardSetUpByHandIsLeftAlone()
    {
        PortForwardService.IsLeftOfNitroxServer("Subnautica").Should().BeFalse();
        PortForwardService.IsLeftOfNitroxServer(null).Should().BeFalse();
    }

    [TestMethod]
    public void MappingIsLeasedNotPermanent()
    {
        Mapping mapping = NatHelper.CreateMapping(11000, 11000, Protocol.Udp, "Nitrox server", NatHelper.LEASE_SECONDS);

        mapping.Lifetime.Should().Be(NatHelper.LEASE_SECONDS);
        NatHelper.LEASE_SECONDS.Should().BeGreaterThan(0, "0 is permanent in UPnP");
    }
}
