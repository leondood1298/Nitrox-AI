using Nitrox.Model.Core;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     A player who leaves before their announced join is acted on must not be created afterwards.
/// </summary>
[TestClass]
public sealed class PlayerManagerTest
{
    [TestMethod]
    public void APlayerWhoLeftAfterTheAnnouncementIsNotCreated()
    {
        PlayerManager playerManager = new(null!, null!, null!);
        SessionId bob = 4;

        playerManager.Announced(bob);
        playerManager.Departed(bob);

        playerManager.HasDeparted(bob).Should().BeTrue();
    }

    [TestMethod]
    public void ANewAnnouncementAfterLeavingCounts()
    {
        PlayerManager playerManager = new(null!, null!, null!);
        SessionId bob = 4;
        playerManager.Departed(bob);

        playerManager.Announced(bob);

        playerManager.HasDeparted(bob).Should().BeFalse();
    }
}
