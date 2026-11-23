using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     Each player's unread PDA markers. A player who plays reports its own; for one who is away the server adds what the
///     game would have marked for an entry another player unlocked.
/// </summary>
[TestClass]
public sealed class PdaNotificationManagerTest
{
    private ServerPlayer playing;
    private ServerPlayer away;
    private ServerPlayer joining;
    private PdaNotificationManager manager;

    [TestInitialize]
    public void Init()
    {
        ConnectedPlayers players = new();
        playing = players.Connect("Playing");
        playing.HasFinishedInitialSync = true;
        away = players.Connect("Away");
        away.HasFinishedInitialSync = true;
        away.IsOnline = false;
        joining = players.Connect("Joining");
        manager = new PdaNotificationManager(players.PlayerManager);
    }

    [TestMethod]
    public void ALogEntryWrittenWhileAPlayerIsAwayIsUnreadForThemWhenTheyComeBack()
    {
        manager.LogEntryAdded("Story_AuroraWarning1");

        away.UnreadPdaNotifications.Should().Equal(new PdaNotification(PdaNotificationGroup.LOG, "Story_AuroraWarning1"));
    }

    [TestMethod]
    public void APlayerWhoPlaysGetsTheMarkerFromTheirOwnGameNotFromTheServer()
    {
        manager.LogEntryAdded("Story_AuroraWarning1");

        playing.UnreadPdaNotifications.Should().BeEmpty();
    }

    [TestMethod]
    public void APlayerStillJoiningKeepsTheMarkerOnTheServer()
    {
        // It was sent its markers when it began joining; this one came after
        manager.EncyclopediaEntryAdded("Peeper", true);

        joining.UnreadPdaNotifications.Should().Equal(new PdaNotification(PdaNotificationGroup.ENCYCLOPEDIA, "Peeper"));
    }

    [TestMethod]
    public void ABlueprintLearnedWhileAwayIsMarkedInEveryPlaceTheGameMarksItKeyedByItsNumber()
    {
        manager.BlueprintLearned(new NitroxTechType("Seaglide"), true);

        string key = ((int)TechType.Seaglide).ToString();
        away.UnreadPdaNotifications.Should().BeEquivalentTo([
            new PdaNotification(PdaNotificationGroup.BLUEPRINTS, key),
            new PdaNotification(PdaNotificationGroup.CRAFT_TREE, key),
            new PdaNotification(PdaNotificationGroup.BUILDER, key)
        ]);
    }

    [TestMethod]
    public void WhatTheGameAddsQuietlyIsNotMarked()
    {
        manager.BlueprintLearned(new NitroxTechType("Seaglide"), false);
        manager.EncyclopediaEntryAdded("Peeper", false);

        away.UnreadPdaNotifications.Should().BeEmpty();
    }

    [TestMethod]
    public void AnUnknownTechTypeIsNotMarked()
    {
        manager.BlueprintLearned(new NitroxTechType("NoSuchTech"), true);

        away.UnreadPdaNotifications.Should().BeEmpty();
    }

    [TestMethod]
    public void AMarkerThePlayerReadIsNoLongerKept()
    {
        PdaNotification entry = new(PdaNotificationGroup.LOG, "Story_AuroraWarning1");
        manager.Change(playing, entry, true);
        manager.Change(playing, new PdaNotification(PdaNotificationGroup.LOG, "Story_AuroraWarning2"), true);

        manager.Change(playing, entry, false);

        playing.UnreadPdaNotifications.Should().Equal(new PdaNotification(PdaNotificationGroup.LOG, "Story_AuroraWarning2"));
    }

    [TestMethod]
    public void AMarkerReadBeforeLeavingStaysReadWhenNothingNewIsUnlocked()
    {
        PdaNotification entry = new(PdaNotificationGroup.LOG, "Story_AuroraWarning1");
        manager.Change(away, entry, true);
        manager.Change(away, entry, false);

        manager.LogEntryAdded("Story_AuroraWarning2");

        away.UnreadPdaNotifications.Should().Equal(new PdaNotification(PdaNotificationGroup.LOG, "Story_AuroraWarning2"));
    }
}
