using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic;

/// <summary>
///     What the server last said about the Lifepod's repairs, kept for the end of the intro, which damages the pod and
///     its radio locally once more. Applying it needs the game; what is kept does not.
/// </summary>
[TestClass]
public sealed class EscapePodRepairStateTest
{
    private readonly EscapePodRepairState state = new();
    private readonly NitroxId podId = new();

    [TestMethod]
    public void NothingIsKnownBeforeTheServerSentAnything()
    {
        Assert.IsFalse(state.TryGetLatest(podId, out _));
    }

    [TestMethod]
    public void TheLatestStateTheServerSentIsKept()
    {
        // A player still in the intro gets the pod in its spawn packet and every repair as an update afterwards.
        state.Remember(podId, new EscapePodMetadata(false, false));
        EscapePodMetadata repaired = new(true, true);
        state.Remember(podId, repaired);

        Assert.IsTrue(state.TryGetLatest(podId, out EscapePodMetadata latest));
        Assert.AreSame(repaired, latest);
    }

    [TestMethod]
    public void AStateIsKeptForThePodItWasSentFor()
    {
        state.Remember(new NitroxId(), new EscapePodMetadata(true, true));

        Assert.IsFalse(state.TryGetLatest(podId, out _));
    }
}
