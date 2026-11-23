using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

[TestClass]
public sealed class PdaManagerTest
{
    private static readonly NitroxTechType fragment = new("SeamothFragment");

    private PdaManager pdaManager;

    [TestInitialize]
    public void Init()
    {
        pdaManager = new PdaManager(NullLogger<PdaManager>.Instance);
    }

    [TestMethod]
    public void AddScannedFragment_TwoScansAtOnce_BothCount()
    {
        pdaManager.AddScannedFragment(fragment, 3, out bool firstResearched).Should().Be(1);
        pdaManager.AddScannedFragment(fragment, 3, out bool secondResearched).Should().Be(2);

        firstResearched.Should().BeFalse();
        secondResearched.Should().BeFalse();
        pdaManager.GetInitialPDAData().ScannerPartial.Should().ContainSingle().Which.Unlocked.Should().Be(2);
    }

    [TestMethod]
    public void AddScannedFragment_ReachingTheTotal_MarksTheTechResearched()
    {
        pdaManager.AddScannedFragment(fragment, 2, out _);

        pdaManager.AddScannedFragment(fragment, 2, out bool fullyResearched).Should().Be(2);

        fullyResearched.Should().BeTrue();
        pdaManager.ContainsCompletelyScannedTech(fragment).Should().BeTrue();
        pdaManager.GetInitialPDAData().ScannerPartial.Should().BeEmpty();
    }

    [TestMethod]
    public void AddScannedFragment_AlreadyResearched_StaysCappedAtTheTotal()
    {
        pdaManager.AddScannedFragment(fragment, 1, out _);

        pdaManager.AddScannedFragment(fragment, 1, out bool fullyResearched).Should().Be(1);

        fullyResearched.Should().BeTrue();
        pdaManager.GetInitialPDAData().ScannerPartial.Should().BeEmpty();
    }
}
