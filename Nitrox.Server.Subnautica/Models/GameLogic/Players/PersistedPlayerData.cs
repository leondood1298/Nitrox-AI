using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Players;

internal sealed class PersistedPlayerData
{
    public string Name { get; set; }

    /// <summary>
    ///     The public key the name is tied to; null for a player saved before such keys existed (including one tied to
    ///     the older kind of key, whose field is no longer read), who keeps the first key they join with.
    /// </summary>
    public string? IdentityPublicKey { get; set; }

    /// <summary>
    ///     The rank given by a command, applied again when the player proves the same key; null for the server's default.
    /// </summary>
    public Perms? GrantedPermissions { get; set; }

    public bool IsMuted { get; set; }

    public List<NitroxTechType> UsedItems { get; set; } = [];

    public Optional<NitroxId>[] QuickSlotsBindingIds { get; set; } = [];

    public Dictionary<string, NitroxId> EquippedItems { get; set; } = [];

    public PeerId Id { get; set; }

    public NitroxVector3 SpawnPosition { get; set; }

    public NitroxQuaternion SpawnRotation { get; set; }

    public PlayerStatsData CurrentStats { get; set; }

    public SubnauticaGameMode GameMode { get; set; }

    public NitroxId SubRootId { get; set; }

    public NitroxId NitroxId { get; set; }

    public bool IsPermaDeath { get; set; }

    /// <summary>
    /// Those goals are unlocked individually (e.g. opening PDA, eating, picking up a fire extinguisher for the first time)
    /// </summary>
    public Dictionary<string, float> PersonalCompletedGoalsWithTimestamp { get; set; } = [];

    public SubnauticaPlayerPreferences? PlayerPreferences { get; set; }

    public bool InPrecursor { get; set; }

    public bool DisplaySurfaceWater { get; set; }

    public List<PdaNotification> UnreadPdaNotifications { get; set; } = [];

    /// <summary>
    ///     The player's death beacons not yet reached.
    /// </summary>
    public List<NitroxVector3> DeathMarkers { get; set; } = [];

    public Player ToPlayer()
    {
        Player player = new(Id,
                          0,
                          Name,
                          IsPermaDeath,
                          null, //no connection/context as this player is not connected.
                          SpawnPosition,
                          SpawnRotation,
                          NitroxId,
                          Optional.OfNullable(SubRootId),
                          // The rank in effect is set when the player joins (PlayerRanks.OnJoin)
                          GrantedPermissions ?? Perms.DEFAULT,
                          CurrentStats,
                          GameMode,
                          UsedItems,
                          QuickSlotsBindingIds,
                          EquippedItems,
                          PersonalCompletedGoalsWithTimestamp,
                          PlayerPreferences.PingPreferences,
                          PlayerPreferences.PinnedTechTypes,
                          InPrecursor,
                          DisplaySurfaceWater);
        foreach (PdaNotification notification in UnreadPdaNotifications ?? [])
        {
            player.UnreadPdaNotifications.Add(notification);
        }
        player.IdentityPublicKey = IdentityPublicKey;
        player.GrantedPermissions = GrantedPermissions;
        player.IsMuted = IsMuted;
        foreach (NitroxVector3 marker in DeathMarkers ?? [])
        {
            player.DeathMarkers.Add(marker);
        }
        return player;
    }

    public static PersistedPlayerData FromPlayer(Player player)
    {
        return new PersistedPlayerData
        {
            Name = player.Name,
            IdentityPublicKey = player.IdentityPublicKey,
            GrantedPermissions = player.GrantedPermissions,
            IsMuted = player.IsMuted,
            UsedItems = player.UsedItems?.ToList(),
            QuickSlotsBindingIds = player.QuickSlotsBindingIds,
            EquippedItems = new(player.EquippedItems),
            Id = player.PeerId,
            SpawnPosition = player.Position,
            SpawnRotation = player.Rotation,
            CurrentStats = player.Stats,
            GameMode = player.GameMode,
            SubRootId = player.SubRootId.OrNull(),
            NitroxId = player.GameObjectId,
            IsPermaDeath = player.IsPermaDeath,
            PersonalCompletedGoalsWithTimestamp = new(player.PersonalCompletedGoalsWithTimestamp),
            PlayerPreferences = new(player.PingInstancePreferences.ToDictionary(m => m.Key, m => m.Value), player.PinnedRecipePreferences.ToList()),
            InPrecursor = player.InPrecursor,
            DisplaySurfaceWater = player.DisplaySurfaceWater,
            UnreadPdaNotifications = player.UnreadPdaNotifications.ToList(),
            DeathMarkers = player.DeathMarkers.ToList()
        };
    }
}
