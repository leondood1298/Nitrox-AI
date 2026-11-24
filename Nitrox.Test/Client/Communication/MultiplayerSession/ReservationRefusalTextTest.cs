using Nitrox.Model.MultiplayerSession;
using NitroxClient.Communication.MultiplayerSession;

namespace Nitrox.Test.Client.Communication.MultiplayerSession;

[TestClass]
public sealed class ReservationRefusalTextTest
{
    private static string Translate(string key) => $"<{key}>";

    [TestMethod]
    public void EveryRefusalReasonIsShownThroughItsLanguageKey()
    {
        foreach (MultiplayerSessionReservationState reason in Enum.GetValues(typeof(MultiplayerSessionReservationState)).Cast<MultiplayerSessionReservationState>()
                                                                     .Where(state => state is not (MultiplayerSessionReservationState.RESERVED or MultiplayerSessionReservationState.REJECTED)))
        {
            string text = ReservationRefusalText.Describe(MultiplayerSessionReservationState.REJECTED | reason, null, Translate);

            text.Should().StartWith("<Nitrox_Refused_").And.NotBe("<Nitrox_Refused_Unknown>", $"{reason} needs its own text");
        }
    }

    [TestMethod]
    public void TheNewerRefusalsHaveTheirOwnTexts()
    {
        ReservationRefusalText.Describe(MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.PLAYER_KEY_MISMATCH, null, Translate)
                              .Should().Be("<Nitrox_Refused_KeyMismatch>");
        ReservationRefusalText.Describe(MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.HARDCORE_PLAYER_DEAD, null, Translate)
                              .Should().Be("<Nitrox_Refused_HardcoreDead>");
    }

    [TestMethod]
    public void TheServersOwnDetailFollowsTheReason()
    {
        ReservationRefusalText.Describe(MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.PLAYER_BANNED, "Griefing", Translate)
                              .Should().Be($"<Nitrox_Refused_Banned>{Environment.NewLine}Griefing");
    }

    [TestMethod]
    public void ARefusalWithoutAKnownReasonStillSaysSomething()
    {
        ReservationRefusalText.Describe(MultiplayerSessionReservationState.REJECTED, null, Translate).Should().Be("<Nitrox_Refused_Unknown>");
    }
}
