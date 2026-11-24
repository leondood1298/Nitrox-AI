using System.Net;
using System.Threading.Tasks;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Services;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Test.Server.Services;

/// <summary>
///     A moderator could lift an admin's ban, and a ban held only the address, so a banned player came back from another one.
/// </summary>
[TestClass]
public sealed class BanServiceTest
{
    [TestMethod]
    public async Task OnlyTheIssuingRankOrHigherLiftsABan()
    {
        BanService bans = new ConnectedPlayers().BanService;
        IPAddress address = IPAddress.Parse("203.0.113.9");
        await bans.BanAsync(address, null, "Bob", "", "Admin", Perms.ADMIN, TimeSpan.Zero);

        BanService.UnbanResult byModerator = await bans.UnbanAsync(entry => address.Equals(entry.IP), Perms.MODERATOR);
        BanService.UnbanResult byAdmin = await bans.UnbanAsync(entry => address.Equals(entry.IP), Perms.ADMIN);

        byModerator.Removed.Should().Be(0);
        byModerator.Refused.Should().ContainSingle();
        byAdmin.Removed.Should().Be(1);
        bans.IsBanned(address).Should().BeFalse();
    }

    [TestMethod]
    public void ABanSavedBeforeRanksWereKeptNeedsAModerator()
    {
        BanService.BanEntry old = new() { IP = IPAddress.Loopback };

        BanService.MayLift(old, Perms.MODERATOR).Should().BeTrue();
        BanService.MayLift(old, Perms.PLAYER).Should().BeFalse();
    }

    [TestMethod]
    public async Task AKeyBanHoldsWithoutAnAddress()
    {
        BanService bans = new ConnectedPlayers().BanService;
        string key = ConnectedPlayers.PublicKeyOf("bobs-key");

        await bans.BanAsync(null, key, "Bob", "", "Admin", Perms.ADMIN, TimeSpan.Zero);

        bans.IsKeyBanned(key).Should().BeTrue();
        bans.IsKeyBanned(ConnectedPlayers.PublicKeyOf("other")).Should().BeFalse();
        bans.GetKeyBanRejectionDetail(key).Should().Contain("permanent");
    }
}
