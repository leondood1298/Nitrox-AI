using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Serialization;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class GameDataTest
{
    private readonly ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

    [TestMethod]
    public void SaveKeepsTheActiveTeleporters()
    {
        GameData saved = new() { ActiveTeleporters = ["crashedship", "prison"] };

        using MemoryStream output = new();
        serializer.Serialize(output, saved);
        using MemoryStream input = new(output.ToArray());
        GameData loaded = serializer.Deserialize<GameData>(input);

        loaded.ActiveTeleporters.Should().Equal("crashedship", "prison");
    }

    [TestMethod]
    public void SaveWithoutTeleportersLoadsWithNoneActive()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes("{}"));

        GameData loaded = serializer.Deserialize<GameData>(input);

        loaded.ActiveTeleporters.Should().BeEmpty();
    }
}
