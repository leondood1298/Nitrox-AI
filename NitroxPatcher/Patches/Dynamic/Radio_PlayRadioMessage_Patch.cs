using System.Reflection;
using NitroxClient.Communication.Abstract;
using Nitrox.Model.Subnautica.Packets;
using Story;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Radio_PlayRadioMessage_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Radio t) => t.PlayRadioMessage());

    public static void Prefix()
    {
        // StoryGoalManager.ExecutePendingRadioMessage plays the head of the queue, or nothing if the queue is empty
        if (StoryGoalManager.main.pendingRadioMessages.Count > 0)
        {
            Resolve<IPacketSender>().Send(new RadioPlayPendingMessage(StoryGoalManager.main.pendingRadioMessages[0]));
        }
    }
}
