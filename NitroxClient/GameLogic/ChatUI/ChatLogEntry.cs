using System;
using UnityEngine;

namespace NitroxClient.GameLogic.ChatUI
{
    public class ChatLogEntry
    {
        public string PlayerName { get; }
        public string MessageText { get; set; }
        public Color32 PlayerColor { get; }
        public string Time { get; set; }
        public GameObject EntryObject { get; set; }

        /// <summary>
        ///     Lines from the server, which may carry formatting; a player's lines are always shown as plain text.
        /// </summary>
        public bool IsServer { get; }

        public int LineCount { get; set; } = 1;

        public ChatLogEntry(string playerName, string messageText, Color32 playerColor, bool isServer = false)
        {
            PlayerName = playerName;
            MessageText = messageText;
            PlayerColor = playerColor;
            IsServer = isServer;
            UpdateTime();
        }

        public void UpdateTime() => Time = DateTime.Now.ToString("HH:mm");
    }
}
