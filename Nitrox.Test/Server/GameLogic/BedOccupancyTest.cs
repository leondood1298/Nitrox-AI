using System.Net;
using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class BedOccupancyTest
{
    [TestMethod]
    public void JoiningPlayerIsToldWhoIsLyingInBedButNotAboutThemselves()
    {
        BedOccupancy occupancy = new();
        NitroxId bed = new();
        SessionId sleeper = 1;
        SessionId joiner = 2;
        occupancy.SetLyingDown(sleeper, bed, "bed_down_left");

        BedEnterAnimation told = occupancy.GetAnnouncementsFor(joiner).Single();

        Assert.AreEqual(sleeper, told.SessionId);
        Assert.AreEqual(bed, told.BedId);
        Assert.AreEqual("bed_down_left", told.AnimationKey);
        Assert.AreEqual(0, occupancy.GetAnnouncementsFor(sleeper).Count);
    }

    [TestMethod]
    public async Task StandingUpOrDisconnectingEndsTheAnnouncement()
    {
        BedOccupancy occupancy = new();
        SessionId first = 1;
        SessionId second = 2;
        SessionId joiner = 3;
        occupancy.SetLyingDown(first, new NitroxId(), "bed_down_left");
        occupancy.SetLyingDown(second, new NitroxId(), "bed_down_right");

        occupancy.SetStoodUp(first);
        Assert.AreEqual(second, occupancy.GetAnnouncementsFor(joiner).Single().SessionId);

        await occupancy.OnEventAsync(new ISessionCleaner.Args(new SessionManager.Session(second, new IPEndPoint(IPAddress.Loopback, 11000)), 1));
        Assert.AreEqual(0, occupancy.GetAnnouncementsFor(joiner).Count, "A player who left is no longer in bed.");
    }
}
