using NitroxClient.GameLogic.ChatUI;
using UnityEngine;

namespace Nitrox.Test.Client.GameLogic.ChatUI;

[TestClass]
public sealed class ChatLogRulesTest
{
    [TestMethod]
    public void APlayerNamedServerNeverJoinsAServerEntry()
    {
        ChatLogEntry serverEntry = new("Server", "Server is restarting...", new Color32(140, 0, 255, 255), true);

        ChatLogRules.JoinsEntry(serverEntry, "Server", false).Should().BeFalse();
        ChatLogRules.JoinsEntry(serverEntry, "Server", true).Should().BeTrue();
    }

    [TestMethod]
    public void AnEntryTakesLinesOfTheSamePlayerOnlyUpToItsLimit()
    {
        ChatLogEntry entry = new("Ana", "hi", new Color32(255, 255, 255, 255));

        ChatLogRules.JoinsEntry(entry, "Ana", false).Should().BeTrue();
        ChatLogRules.JoinsEntry(entry, "Bob", false).Should().BeFalse();

        entry.LineCount = ChatLogRules.MAX_LINES_PER_ENTRY;
        ChatLogRules.JoinsEntry(entry, "Ana", false).Should().BeFalse("a new entry starts instead of one text growing without end");
    }

    [TestMethod]
    public void EveryLineIsCutToTheLineLimitAlsoWhenItJoinsAnEntry()
    {
        string longLine = new('a', ChatLogRules.LINE_CHAR_LIMIT + 50);

        ChatLogRules.Sanitize(longLine).Length.Should().Be(ChatLogRules.LINE_CHAR_LIMIT);
        ChatLogRules.Sanitize("  hello \n").Should().Be("hello");
        ChatLogRules.Sanitize(null).Should().BeEmpty();
    }

    [TestMethod]
    public void NothingBeforeAnyEntryIsJoined()
    {
        ChatLogRules.JoinsEntry(null, "Ana", false).Should().BeFalse();
    }
}
