using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Nitrox.Model.Constants;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Serialization;

namespace Nitrox.Model.Configuration;

/// <summary>
///     Options that are tied to a hosted Subnautica world.
/// </summary>
[SerializableFileName("server.cfg")]
[PropertyDescription("Server settings can be changed here")]
public sealed partial class SubnauticaServerOptions
{
    public const string CONFIG_SECTION_PATH = "GameServer";

    [Range(1, byte.MaxValue)]
    public byte MaxConnections { get; set; } = 100;

    [Range(1, ushort.MaxValue)]
    public ushort ServerPort { get; set; } = SubnauticaServerConstants.DEFAULT_PORT;

    /// <remarks>
    ///     Any text is allowed: the password is only compared, so spaces and symbols are fine. A stricter rule here made
    ///     the server refuse to start with a password the launcher and the change command had accepted.
    /// </remarks>
    [PropertyDescription("If not empty, users will be asked to enter a password to join the server")]
    public string ServerPassword { get; set; } = "";

    public SubnauticaGameMode GameMode { get; set; }

    [PropertyDescription("Measured in milliseconds. Values less than 1 second will disable auto saving.")]
    public int SaveInterval { get; set; } = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

    [PropertyDescription("Backups kept as they are after each save. Older ones are thinned to one per hour for a day, then one per day for a week. 0 turns backups off.")]
    [Range(0, int.MaxValue)]
    public int MaxBackups { get; set; } = 10;

    [PropertyDescription("Minutes at least between two backups. A save in between is not backed up. 0 backs up after every save that changed the world.")]
    [Range(0, int.MaxValue)]
    public int BackupIntervalMinutes { get; set; } = 15;

    [Range(30001, int.MaxValue)]
    public int InitialSyncTimeout { get; set; } = 300000;

    [PropertyDescription("Possible values:", typeof(Perms))]
    [EnumDataType(typeof(Perms))]
    public Perms DefaultPlayerPerm { get; set; } = Perms.DEFAULT;

    [PropertyDescription("If true, a player joining from 127.0.0.1 (this computer) gets admin. WARNING: a tunnel or proxy program running on this computer (playit and the like) makes every player it forwards come from 127.0.0.1, so all of them would get admin - turn this off if you use one.")]
    public bool LocalhostIsAdmin { get; set; } = true;

    public float DefaultOxygenValue { get; set; } = 45;

    public float DefaultMaxOxygenValue { get; set; } = 45;
    public float DefaultHealthValue { get; set; } = 80;
    public float DefaultHungerValue { get; set; } = 50.5f;
    public float DefaultThirstValue { get; set; } = 90.5f;

    [PropertyDescription("Recommended to keep at 0.1 which is the default starting value. If set to 0, new players are cured by default.")]
    public float DefaultInfectionValue { get; set; } = 0.1f;

    [PropertyDescription("If set to true, the server will try to open ports on your router via UPnP")]
    public bool PortForward { get; set; } = true;

    [PropertyDescription("Determines whether the server will listen for and reply to LAN discovery requests.")]
    public bool LanDiscovery { get; set; } = true;

    [PropertyDescription("If true, players who run Discord may publish this server's internet address so their Discord friends can ask to join. The address then reaches anyone a player accepts.")]
    public bool AllowDiscordInvites { get; set; } = false;

    [PropertyDescription("Set to true to Cache entities for the whole map on next run. \nWARNING! Will make server load take longer on the cache run but players will gain a performance boost when entering new areas.")]
    public bool CreateFullEntityCache { get; set; } = false;

    [Required]
    [RegularExpression(@"\w+")]
    public string Seed { get; set; } = "";

    [PropertyDescription("If true, players below admin cannot open the game's developer console. Admins always can.")]
    public bool DisableConsole { get; set; }

    [PropertyDescription("Password for the /login chat command, which makes a player admin. Empty turns /login off.")]
    public string? AdminPassword { get; set; } = "";
    public bool KeepInventoryOnDeath { get; set; }

    [PropertyDescription("Places a beacon where players die")]
    public bool MarkDeathPointsWithBeacon { get; set; }
    public bool PvpEnabled { get; set; } = true;
    public bool AutoSave { get; set; } = true;

    [PropertyDescription("When true, will reject any build actions detected as desynced")]
    public bool SafeBuilding { get; set; } = true;

    [PropertyDescription("Command to run following a successful world save (e.g. .exe, .bat, or PowerShell script). ")]
    public string PostSaveCommandPath { get; set; } = "";

    [PropertyDescription("Largest picture, in kilobytes, a player may put into a picture frame. 0 turns shared pictures off.")]
    [Range(0, 16384)]
    public int PictureFrameMaxKilobytes { get; set; } = 2048;

    [PropertyDescription("Room, in megabytes, all the pictures in this world's picture frames may take together")]
    [Range(0, 4096)]
    public int PictureFramesMaxTotalMegabytes { get; set; } = 64;

    [OptionsValidator]
    public partial class Validator : IValidateOptions<SubnauticaServerOptions>;
}
