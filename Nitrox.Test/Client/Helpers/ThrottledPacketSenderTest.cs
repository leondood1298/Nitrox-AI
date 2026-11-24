using System.Collections.Generic;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Helpers;

namespace Nitrox.Test.Client.Helpers;

[TestClass]
public sealed class ThrottledPacketSenderTest
{
    private const float THROTTLE = 0.2f;

    private readonly RecordingSender sender = new();
    private ThrottledPacketSender throttled = null!;
    private DateTime now;

    [TestInitialize]
    public void Setup()
    {
        now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        throttled = new ThrottledPacketSender(sender) { Clock = () => now };
    }

    [TestMethod]
    public void TheFirstPacketGoesAtOnceAndALaterOneWhenTheTimeIsUp()
    {
        throttled.SendThrottled(Chat("a"), _ => "key", THROTTLE);
        throttled.SendThrottled(Chat("b"), _ => "key", THROTTLE);
        Advance(0.1f);
        throttled.Update();

        sender.Texts.Should().Equal("a");

        Advance(0.15f);
        throttled.Update();

        sender.Texts.Should().Equal("a", "b");
    }

    [TestMethod]
    public void APacketRightAfterOneSentFromWaitingWaitsAWholeThrottleTime()
    {
        throttled.SendThrottled(Chat("a"), _ => "key", THROTTLE);
        throttled.SendThrottled(Chat("b"), _ => "key", THROTTLE);
        Advance(0.25f);
        throttled.Update();
        throttled.SendThrottled(Chat("c"), _ => "key", THROTTLE);
        Advance(0.1f);
        throttled.Update();

        sender.Texts.Should().Equal("a", "b");

        Advance(0.15f);
        throttled.Update();

        sender.Texts.Should().Equal("a", "b", "c");
    }

    [TestMethod]
    public void AKeyIsForgottenOnceItsTimeIsUpWithNothingWaiting()
    {
        for (int i = 0; i < 100; i++)
        {
            throttled.SendThrottled(Chat($"creature {i}"), packet => packet.Text, THROTTLE);
        }
        throttled.Count.Should().Be(100);

        Advance(0.25f);
        throttled.Update();

        throttled.Count.Should().Be(0);
    }

    [TestMethod]
    public void AKeyWithAPacketWaitingIsKeptUntilItIsSent()
    {
        throttled.SendThrottled(Chat("a"), _ => "key", THROTTLE);
        throttled.SendThrottled(Chat("b"), _ => "key", THROTTLE);
        Advance(0.25f);
        throttled.Update();

        throttled.Count.Should().Be(1);
        sender.Texts.Should().Equal("a", "b");

        Advance(0.25f);
        throttled.Update();

        throttled.Count.Should().Be(0);
    }

    [TestMethod]
    public void AfterAQuietTimeTheNextPacketGoesAtOnce()
    {
        throttled.SendThrottled(Chat("a"), _ => "key", THROTTLE);
        Advance(1f);
        throttled.Update();

        throttled.SendThrottled(Chat("b"), _ => "key", THROTTLE);

        sender.Texts.Should().Equal("a", "b");
    }

    private void Advance(float seconds) => now = now.AddSeconds(seconds);

    private static ChatMessage Chat(string text) => new((ushort)1, text);

    private sealed class RecordingSender : IPacketSender
    {
        public List<string> Texts { get; } = [];

        public bool Send<T>(T packet) where T : Packet
        {
            Texts.Add(((ChatMessage)(object)packet).Text);
            return true;
        }
    }
}
