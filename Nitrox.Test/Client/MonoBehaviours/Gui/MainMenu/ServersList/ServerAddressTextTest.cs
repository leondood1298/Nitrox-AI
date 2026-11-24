using NitroxClient.MonoBehaviours.Gui.MainMenu.ServersList;

namespace Nitrox.Test.Client.MonoBehaviours.Gui.MainMenu.ServersList;

[TestClass]
public sealed class ServerAddressTextTest
{
    [TestMethod]
    public void WithHideIpOnNoPartOfTheAddressIsShown()
    {
        string text = ServerAddressText.Format("203.0.113.7", 11000, true);

        text.Should().NotContain("203").And.NotContain("11000");
    }

    [TestMethod]
    public void WithHideIpOffTheAddressAndPortAreShown()
    {
        ServerAddressText.Format("203.0.113.7", 11000, false).Should().Be("203.0.113.7:11000");
    }
}
