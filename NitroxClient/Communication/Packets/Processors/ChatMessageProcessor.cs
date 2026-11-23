using System;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.ChatUI;
using NitroxClient.GameLogic.Settings;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class ChatMessageProcessor(PlayerManager remotePlayerManager, LocalPlayer localPlayer) : IClientPacketProcessor<ChatMessage>
{
    private readonly LocalPlayer localPlayer = localPlayer;
    private readonly PlayerChatManager playerChatManager = PlayerChatManager.Instance;
    private readonly PlayerManager remotePlayerManager = remotePlayerManager;

    private readonly Color32 serverMessageColor = new Color32(0x8c, 0x00, 0xFF, 0xFF);

    public Task Process(ClientProcessorContext context, ChatMessage message)
    {
        if (message.SessionId != SessionId.SERVER_ID)
        {
            LogClientMessage(message);
        }
        else
        {
            LogServerMessage(message);
        }
        return Task.CompletedTask;
    }

    private void LogClientMessage(ChatMessage message)
    {
        // The message can come from either the local player or other players
        string playerName;
        NitroxColor color;
        if (localPlayer.SessionId == message.SessionId)
        {
            playerName = localPlayer.PlayerName;
            color = localPlayer.PlayerSettings.PlayerColor;
        }
        else
        {
            Optional<RemotePlayer> remotePlayer = remotePlayerManager.Find(message.SessionId);
            if (remotePlayer.HasValue)
            {
                playerName = remotePlayer.Value.PlayerName;
                color = remotePlayer.Value.PlayerSettings.PlayerColor;
            }
            else
            {
                // A player this game does not know (yet): the message is still shown rather than lost
                Log.Warn($"Chat message from unknown session #{message.SessionId}");
                playerName = $"#{message.SessionId}";
                color = new NitroxColor(1f, 1f, 1f);
            }
        }

        playerChatManager.AddMessage(playerName, message.Text, color.ToUnity());
        if (NitroxPrefs.ChatAutoOpen.Value)
        {
            playerChatManager.ShowChat();
        }
    }

    private void LogServerMessage(ChatMessage message)
    {
        playerChatManager.AddMessage("Server", message.Text, serverMessageColor);
        if (NitroxPrefs.ChatAutoOpen.Value)
        {
            playerChatManager.ShowChat();
        }
    }
}
