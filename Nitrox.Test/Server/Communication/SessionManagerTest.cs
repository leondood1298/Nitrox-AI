using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;

namespace Nitrox.Test.Server.Communication;

[TestClass]
public sealed class SessionManagerTest
{
    [TestMethod]
    public async Task IdsWrapWithoutGivingOutTheServersIdOrOneInUse()
    {
        SessionManager sessions = new(new ISessionCleaner.Trigger(() => []), NullLogger<SessionManager>.Instance);
        SessionManager.Session kept = sessions.GetOrCreateSession(new IPEndPoint(IPAddress.Loopback, 1));

        // A script connecting and leaving 65535 times within the reuse delay
        HashSet<SessionId> seen = [kept.Id];
        for (int i = 0; i < ushort.MaxValue - 1; i++)
        {
            SessionManager.Session session = sessions.GetOrCreateSession(new IPEndPoint(IPAddress.Loopback, 2));
            session.Id.IsPlayer.Should().BeTrue();
            seen.Add(session.Id);
            await sessions.RemoveSessionAsync(session.Id);
        }

        seen.Should().HaveCount(ushort.MaxValue, "every id was handed out once, none twice and never the server's");
        Action exhausted = () => sessions.GetOrCreateSession(new IPEndPoint(IPAddress.Loopback, 3));
        exhausted.Should().Throw<InvalidOperationException>("all other ids wait out their reuse delay");
    }
}
