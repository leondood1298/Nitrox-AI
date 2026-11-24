namespace NitroxClient.GameLogic.Settings;

[TestClass]
public class PdaPauseOverrideTest
{
    private bool pdaPause;
    private PdaPauseOverride pdaOverride = null!;

    [TestInitialize]
    public void Setup()
    {
        pdaPause = true;
        pdaOverride = new PdaPauseOverride(() => pdaPause, value => pdaPause = value);
    }

    [TestMethod]
    public void ASessionTurnsPdaPauseOffAndGivesThePlayersSettingBackAfter()
    {
        pdaOverride.Begin();
        pdaPause.Should().BeFalse();

        pdaOverride.End();
        pdaPause.Should().BeTrue();
    }

    [TestMethod]
    public void SavingOptionsDuringASessionWritesThePlayersOwnSetting()
    {
        pdaOverride.Begin();
        bool saved = false;

        pdaOverride.BeforeSettingsSerialized();
        saved = pdaPause;
        pdaOverride.AfterSettingsSerialized();

        saved.Should().BeTrue();
        pdaPause.Should().BeFalse();
    }

    [TestMethod]
    public void LoadingOptionsDuringASessionKeepsTheLoadedValueForAfterTheSession()
    {
        pdaOverride.Begin();

        pdaOverride.BeforeSettingsSerialized();
        pdaPause = false; // the file says off
        pdaOverride.AfterSettingsSerialized();
        pdaOverride.End();

        pdaPause.Should().BeFalse();
    }

    [TestMethod]
    public void OutsideASessionNothingChanges()
    {
        pdaOverride.BeforeSettingsSerialized();
        pdaOverride.AfterSettingsSerialized();
        pdaOverride.End();

        pdaPause.Should().BeTrue();
        pdaOverride.IsActive.Should().BeFalse();
    }
}
