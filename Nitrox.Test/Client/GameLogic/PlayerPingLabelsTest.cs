using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class PlayerPingLabelsTest
{
    [TestMethod]
    public void ATechTypeIsNamedInTheReadersLanguage()
    {
        string label = PlayerPingLabels.Build("{PLAYER} a marqué {TARGET}", "Ana", "Peeper", "Peeper", techType => techType == "Peeper" ? "Pipeur" : techType);

        label.Should().Be("Ana a marqué Pipeur");
    }

    [TestMethod]
    public void ATargetWithoutTechTypeKeepsTheSendersName()
    {
        string label = PlayerPingLabels.Build("{PLAYER} pinged {TARGET}", "Ana", "", "Safe Shallows Biome", _ => "unused");

        label.Should().Be("Ana pinged Safe Shallows Biome");
    }
}
