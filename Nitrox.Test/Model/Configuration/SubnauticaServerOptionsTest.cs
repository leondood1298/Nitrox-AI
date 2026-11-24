using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures.GameLogic;

namespace Nitrox.Test.Model.Configuration;

[TestClass]
public sealed class SubnauticaServerOptionsTest
{
    [TestMethod]
    public void AServerPasswordWithSpacesAndSymbolsDoesNotStopTheServerFromStarting()
    {
        SubnauticaServerOptions options = new() { Seed = "SEED", ServerPassword = "my pass-word!" };

        ValidateOptionsResult result = new SubnauticaServerOptions.Validator().Validate(null, options);

        result.Succeeded.Should().BeTrue(result.FailureMessage);
    }

    [TestMethod]
    public void AnUndefinedDefaultRankIsRefused()
    {
        SubnauticaServerOptions options = new() { Seed = "SEED", DefaultPlayerPerm = (Perms)9 };

        new SubnauticaServerOptions.Validator().Validate(null, options).Failed.Should().BeTrue();
    }
}
