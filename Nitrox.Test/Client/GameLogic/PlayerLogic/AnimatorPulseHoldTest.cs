using Nitrox.Model.GameLogic.PlayerAnimation;
using NitroxClient.GameLogic.PlayerLogic;

namespace Nitrox.Test.Client.GameLogic.PlayerLogic;

[TestClass]
public sealed class AnimatorPulseHoldTest
{
    private const float MINIMUM_ON_SECONDS = 0.1f;

    [TestMethod]
    public void SwingThatArrivesAsOnAndOffInTheSameFrameStaysOnLongEnoughToBeSeen()
    {
        AnimatorPulseHold hold = new(MINIMUM_ON_SECONDS);

        Assert.IsTrue(hold.Request("using_tool", true, 10f), "Switching on is never delayed.");
        Assert.IsFalse(hold.Request("using_tool", false, 10f), "Switching off in the same frame has to wait.");

        Assert.AreEqual(0, hold.TakeDue(10.05f).Count);
        CollectionAssert.AreEqual(new List<string> { "using_tool" }, hold.TakeDue(10.15f));
        Assert.AreEqual(0, hold.TakeDue(10.3f).Count, "The delayed switch-off is handed out once.");
    }

    [TestMethod]
    public void SwitchThatWasOnForLongEnoughGoesOffAtOnce()
    {
        AnimatorPulseHold hold = new(MINIMUM_ON_SECONDS);
        hold.Request("using_pda", true, 10f);

        Assert.IsTrue(hold.Request("using_pda", false, 14f));
        Assert.AreEqual(0, hold.TakeDue(15f).Count);
    }

    [TestMethod]
    public void SwitchingOnAgainCancelsAWaitingSwitchOff()
    {
        // Two knife swings in quick succession: the second one must not be cut short by the first one ending.
        AnimatorPulseHold hold = new(MINIMUM_ON_SECONDS);
        hold.Request("using_tool", true, 10f);
        hold.Request("using_tool", false, 10.01f);

        Assert.IsTrue(hold.Request("using_tool", true, 10.05f));

        Assert.AreEqual(0, hold.TakeDue(10.12f).Count, "The first swing ending no longer switches it off.");
        Assert.IsFalse(hold.Request("using_tool", false, 10.13f));
        Assert.AreEqual(0, hold.TakeDue(10.14f).Count);
        CollectionAssert.AreEqual(new List<string> { "using_tool" }, hold.TakeDue(10.2f));
    }

    [TestMethod]
    public void SwitchesAreHeldIndependently()
    {
        AnimatorPulseHold hold = new(MINIMUM_ON_SECONDS);
        hold.Request("jump", true, 10f);
        hold.Request("bash", true, 10.08f);
        hold.Request("jump", false, 10.09f);
        hold.Request("bash", false, 10.09f);

        CollectionAssert.AreEqual(new List<string> { "jump" }, hold.TakeDue(10.14f));
        CollectionAssert.AreEqual(new List<string> { "bash" }, hold.TakeDue(10.25f));
    }

    [TestMethod]
    public void SwitchingOffSomethingThatWasNeverOnIsPassedOn()
    {
        Assert.IsTrue(new AnimatorPulseHold(MINIMUM_ON_SECONDS).Request("using_tool", false, 10f));
    }

    [TestMethod]
    public void ClearingForgetsWaitingSwitchOffs()
    {
        // The animator is reset when a player respawns; nothing may be applied to it afterwards.
        AnimatorPulseHold hold = new(MINIMUM_ON_SECONDS);
        hold.Request("using_tool", true, 10f);
        hold.Request("using_tool", false, 10f);

        hold.Clear();

        Assert.AreEqual(0, hold.TakeDue(11f).Count);
    }

    [TestMethod]
    public void EveryAnimationOtherThanTheTwoHandledSeparatelyHasExactlyOneAnimatorParameter()
    {
        foreach (AnimChangeType type in Enum.GetValues<AnimChangeType>())
        {
            bool isSwitch = PlayerAnimationParameters.Switches.ContainsKey(type);
            bool isTrigger = PlayerAnimationParameters.Triggers.ContainsKey(type);

            if (type is AnimChangeType.UNDERWATER or AnimChangeType.INFECTION_REVEAL)
            {
                Assert.IsFalse(isSwitch || isTrigger, $"{type} is applied by RemotePlayer itself.");
            }
            else
            {
                Assert.IsTrue(isSwitch != isTrigger, $"{type} must be either a switch or a trigger.");
            }
        }
    }
}
