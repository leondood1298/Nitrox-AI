using Nitrox.Model.DataStructures;
using NitroxClient.Communication.MultiplayerSession;

namespace Nitrox.Test.Client.Communication.MultiplayerSession;

[TestClass]
public sealed class JoinPasswordTest
{
    [TestMethod]
    public void AServerWithoutAPasswordIsNeverSentOne()
    {
        JoinPassword.ToSend(false, Optional.Of("password of another server")).HasValue.Should().BeFalse();
    }

    [TestMethod]
    public void AServerThatAsksForAPasswordGetsTheOneTypedForIt()
    {
        Optional<string> sent = JoinPassword.ToSend(true, Optional.Of("secret"));

        sent.HasValue.Should().BeTrue();
        sent.Value.Should().Be("secret");
    }
}
