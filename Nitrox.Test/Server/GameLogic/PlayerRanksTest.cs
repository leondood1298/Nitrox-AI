using System.Net;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Helper;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class PlayerRanksTest
{
    [TestMethod]
    public void AGivenRankNeedsTheKeyProvenAgain()
    {
        PlayerRanks.OnJoin(true, Perms.ADMIN, Perms.PLAYER, false, null).Should().Be(Perms.ADMIN);
        PlayerRanks.OnJoin(false, Perms.ADMIN, Perms.PLAYER, false, null).Should().Be(Perms.PLAYER);
        PlayerRanks.OnJoin(true, null, Perms.MODERATOR, false, null).Should().Be(Perms.MODERATOR);
    }

    [TestMethod]
    public void OnlyTheLoopbackAddressCountsAsTheHost()
    {
        PlayerRanks.OnJoin(false, null, Perms.PLAYER, true, IPAddress.Loopback).Should().Be(Perms.ADMIN);
        PlayerRanks.OnJoin(false, null, Perms.PLAYER, true, IPAddress.Parse("::ffff:127.0.0.1")).Should().Be(Perms.ADMIN);
        // The host's own network address is also what other players on that network, or arriving through the router, may appear as
        PlayerRanks.OnJoin(false, null, Perms.PLAYER, true, NetHelper.GetLanUsableIp()).Should().Be(Perms.PLAYER);
        PlayerRanks.OnJoin(false, null, Perms.PLAYER, false, IPAddress.Loopback).Should().Be(Perms.PLAYER);
    }

    [TestMethod]
    public void LocalhostNeverLowersAHigherRank()
    {
        PlayerRanks.OnJoin(true, Perms.HOST, Perms.PLAYER, true, IPAddress.Loopback).Should().Be(Perms.HOST);
    }

    [TestMethod]
    public void OnlyALowerRankCanBeActedOn()
    {
        PlayerRanks.MayActOn(Perms.MODERATOR, Perms.PLAYER).Should().BeTrue();
        PlayerRanks.MayActOn(Perms.MODERATOR, Perms.MODERATOR).Should().BeFalse();
        PlayerRanks.MayActOn(Perms.ADMIN, Perms.HOST).Should().BeFalse();
    }
}
