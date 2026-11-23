using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class FireDouseLedgerTest
{
    [TestMethod]
    public void ExtinguisherHitsAddUpAndAreSentInFullOnceOverTheThreshold()
    {
        // Each hit is 0.15 to 0.25. Other players must receive everything that was doused, not just the last hit.
        FireDouseLedger ledger = new();
        NitroxId fire = new();
        float sent = 0f;
        int packets = 0;

        for (int hit = 0; hit < 60; hit++)
        {
            float amount = ledger.Record(fire, 0.2f, false);
            if (amount > 0f)
            {
                packets++;
                sent += amount;
            }
        }

        Assert.AreEqual(2, packets, "Sixty hits of 0.2 cross the threshold of 5 twice.");
        Assert.IsTrue(sent > FireDouseLedger.REPORT_THRESHOLD * 2, $"Expected over 10 doused to be sent, got {sent}.");
        Assert.IsTrue(sent <= 12f + 0.001f, "No more than what was doused may be sent.");
    }

    [TestMethod]
    public void ExtinguishingAlwaysSendsWhatIsPending()
    {
        // The fire went out here after only a little dousing since the last packet. It must go out for everyone.
        FireDouseLedger ledger = new();
        NitroxId fire = new();
        Assert.AreEqual(0f, ledger.Record(fire, 0.2f, false));
        Assert.AreEqual(0f, ledger.Record(fire, 0.2f, false));

        float sent = ledger.Record(fire, Fires.EXTINGUISH_DOUSE_AMOUNT, true);

        Assert.AreEqual(Fires.EXTINGUISH_DOUSE_AMOUNT + 0.4f, sent, 0.001f);
        Assert.AreEqual(0f, ledger.Record(fire, 0.2f, false), "Nothing is left over for a fire that went out.");
    }

    [TestMethod]
    public void ExtinguishingOnTheFirstHitIsSent()
    {
        FireDouseLedger ledger = new();

        Assert.AreEqual(Fires.EXTINGUISH_DOUSE_AMOUNT, ledger.Record(new NitroxId(), Fires.EXTINGUISH_DOUSE_AMOUNT, true));
    }

    [TestMethod]
    public void FiresAreTrackedSeparately()
    {
        FireDouseLedger ledger = new();
        NitroxId first = new();
        NitroxId second = new();

        Assert.AreEqual(0f, ledger.Record(first, 4f, false));
        Assert.AreEqual(0f, ledger.Record(second, 4f, false));
        Assert.AreEqual(6f, ledger.Record(first, 2f, false));
        Assert.AreEqual(0f, ledger.Record(second, 0.5f, false));
    }
}
