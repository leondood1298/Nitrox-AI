using System.Threading.Tasks;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Commands;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     The players' games must get the state the server switched to, not the one it had before.
/// </summary>
[TestClass]
public sealed class FastCommandTest
{
    [TestMethod]
    public async Task EnablingSendsTheNewState()
    {
        SessionSettings settings = new();
        RecordingCommandContext context = new();

        await new FastCommand(settings).Execute(context, FastCheatChanged.FastCheat.HATCH, true);

        settings.FastHatch.Should().BeTrue();
        context.SentToAll.OfType<FastCheatChanged>().Should().ContainSingle(packet => packet.Cheat == FastCheatChanged.FastCheat.HATCH && packet.Value);
        context.SentToAll.OfType<string>().Should().ContainSingle(message => message.Contains("changed to True"));
    }

    [TestMethod]
    public async Task TogglingOffSendsTheNewState()
    {
        SessionSettings settings = new() { FastGrow = true };
        RecordingCommandContext context = new();

        await new FastCommand(settings).Execute(context, FastCheatChanged.FastCheat.GROW);

        settings.FastGrow.Should().BeFalse();
        context.SentToAll.OfType<FastCheatChanged>().Should().ContainSingle(packet => !packet.Value);
    }
}
