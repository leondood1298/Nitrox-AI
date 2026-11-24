using Nitrox.Model.DataStructures.GameLogic;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     Moderators had the console while its commands that reach the server needed admin; the server's console switch also
///     locked admins out; and commands that change only one game (terrain, doors, story events) split the shared world.
/// </summary>
[TestClass]
public sealed class ConsoleCommandPolicyTest
{
    [TestMethod]
    public void TheConsoleNeedsAdmin()
    {
        ConsoleCommandPolicy.MaySubmit(Perms.MODERATOR).Should().BeFalse();
        ConsoleCommandPolicy.MaySubmit(Perms.ADMIN).Should().BeTrue();
    }

    [TestMethod]
    public void TheServersSwitchHoldsOnlyBelowAdmin()
    {
        ConsoleCommandPolicy.IsConsoleAllowed(true, Perms.MODERATOR).Should().BeFalse();
        ConsoleCommandPolicy.IsConsoleAllowed(true, Perms.ADMIN).Should().BeTrue();
        ConsoleCommandPolicy.IsConsoleAllowed(false, Perms.PLAYER).Should().BeTrue("submitting is refused later, by rank");
    }

    [TestMethod]
    public void CommandsThatChangeOnlyThisGameAreRefused()
    {
        ConsoleCommandPolicy.IsRefused("dig 5").Should().BeTrue();
        ConsoleCommandPolicy.IsRefused("DayNight 0.5").Should().BeTrue();
        ConsoleCommandPolicy.IsRefused("  unlockdoors").Should().BeTrue();
        ConsoleCommandPolicy.IsRefused("spawn seamoth").Should().BeFalse();
        ConsoleCommandPolicy.IsRefused("item titanium 5").Should().BeFalse();
    }
}
