using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class BasePowerConsumptionLedgerTest
{
    [TestMethod]
    public void PerFrameDrawIsReportedAsOneAmountPerInterval()
    {
        // A charger draws a little every frame. Sixty frames must not become sixty packets.
        BasePowerConsumptionLedger ledger = new();
        NitroxId reactor = new();
        int reports = 0;
        float reported = 0f;

        for (int frame = 0; frame < 60; frame++)
        {
            ledger.Record(reactor, 0.01f);
            foreach (KeyValuePair<NitroxId, float> due in ledger.TakeDue(10f + frame / 60f))
            {
                reports++;
                reported += due.Value;
            }
        }

        Assert.IsTrue(reports is >= 3 and <= 5, $"Expected about four reports in one second, got {reports}.");
        float stillPending = ledger.TakeDue(100f).Sum(due => due.Value);
        Assert.AreEqual(0.6f, reported + stillPending, 0.0001f, "Every unit drawn is reported exactly once.");
    }

    [TestMethod]
    public void SourcesAreReportedSeparately()
    {
        BasePowerConsumptionLedger ledger = new();
        NitroxId first = new();
        NitroxId second = new();
        ledger.Record(first, 2f);
        ledger.Record(second, 3f);
        ledger.Record(first, 1f);

        Dictionary<NitroxId, float> due = ledger.TakeDue(1f).ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.AreEqual(3f, due[first]);
        Assert.AreEqual(3f, due[second]);
        Assert.AreEqual(0, ledger.TakeDue(50f).Count, "Reported amounts are not reported again.");
    }

    [TestMethod]
    public void TinyAndInvalidAmountsAreNotSentOnTheirOwn()
    {
        BasePowerConsumptionLedger ledger = new();
        NitroxId reactor = new();
        ledger.Record(reactor, 0f);
        ledger.Record(reactor, -5f);
        ledger.Record(reactor, float.NaN);
        ledger.Record(reactor, BasePowerConsumptionLedger.MIN_REPORT_AMOUNT / 4f);

        Assert.AreEqual(0, ledger.TakeDue(1f).Count);

        ledger.Record(reactor, BasePowerConsumptionLedger.MIN_REPORT_AMOUNT);
        Assert.AreEqual(BasePowerConsumptionLedger.MIN_REPORT_AMOUNT * 1.25f, ledger.TakeDue(2f).Single().Value, 0.00001f,
            "The small remainder is carried into the next report instead of being lost.");
    }
}
