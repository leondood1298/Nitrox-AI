using System;
using System.Collections;
using System.Globalization;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     Shows how much of a large first sync has arrived. For a world with large bases it can take a while, and the
///     loading screen said nothing meanwhile.
/// </summary>
internal sealed class InitialSyncTransferProcessor(IClient client) : IClientPacketProcessor<InitialSyncTransfer>
{
    private readonly IClient client = client;

    public Task Process(ClientProcessorContext context, InitialSyncTransfer packet)
    {
        Multiplayer.Main.StartCoroutine(ShowProgress(packet.Bytes));
        return Task.CompletedTask;
    }

    private IEnumerator ShowProgress(int bytes)
    {
        long start = client.BytesReceived;
        WaitScreen.ManualWaitItem item = WaitScreen.Add(Text(bytes));
        try
        {
            float fraction = 0;
            while (fraction < 1f && client.IsConnected)
            {
                fraction = Fraction(start, client.BytesReceived, bytes);
                item.SetProgress(fraction);
                yield return null;
            }
        }
        finally
        {
            WaitScreen.Remove(item);
        }
    }

    /// <summary>
    ///     How much of <paramref name="bytes" /> has arrived, from the bytes received when it was announced and now. The
    ///     count includes LiteNetLib's headers, so it reaches the end a little before the last byte; that is close enough.
    /// </summary>
    internal static float Fraction(long receivedAtStart, long receivedNow, int bytes) => bytes <= 0 ? 1f : Math.Min(1f, Math.Max(0f, (receivedNow - receivedAtStart) / (float)bytes));

    private static string Text(int bytes) => Language.main.Get("Nitrox_ReceivingWorld")
                                                     .Replace("{SIZE}", (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture));
}
