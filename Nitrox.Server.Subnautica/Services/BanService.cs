using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.AppEvents.Core;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Server.Subnautica.Services;

/// <summary>
///     Tracks bans. A ban holds an IP address, a player's key (see <see cref="Nitrox.Model.MultiplayerSession.PlayerIdentityProof" />),
///     or both: the key keeps the player out from another address, the address keeps out a new key from the same place.
///     Keeps its own save file (independent from <see cref="WorldService" />'s save/load cycle) so that old saves created
///     before this feature existed keep loading fine.
/// </summary>
internal sealed class BanService(ServerJsonSerializer serializer, IOptions<ServerStartOptions> startOptions, ILogger<BanService> logger) : BackgroundService, ISaveState
{
    /// <summary>
    ///     The rank assumed for a ban saved before the issuer's rank was kept: every ban needed at least a moderator.
    /// </summary>
    internal const Perms UNKNOWN_ISSUER_RANK = Perms.MODERATOR;

    private readonly List<BanEntry> bans = [];
    private readonly Lock bansLock = new();
    private readonly SemaphoreSlim expiryReschedule = new(0, 1);
    private readonly TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private string FilePath => Path.Combine(startOptions.Value.GetServerSavePath(), $"Bans{serializer.FileEnding}");

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(Load, cancellationToken);
        }
        finally
        {
            loaded.TrySetResult();
        }
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    ///     Returns whether the given IP address currently has an active (non-expired) ban.
    /// </summary>
    public bool IsBanned(IPAddress ip) => FindActive(entry => ip.Equals(entry.IP)) != null;

    /// <summary>
    ///     Returns whether the player key currently has an active ban.
    /// </summary>
    public bool IsKeyBanned(string? publicKey) => publicKey != null && FindActive(entry => entry.PlayerPublicKey == publicKey) != null;

    /// <summary>
    ///     Returns extra detail to show a banned player (the ban reason and, for a temporary ban, when it expires),
    ///     or <see langword="null" /> when the IP has no active ban.
    /// </summary>
    public string? GetBanRejectionDetail(IPAddress ip) => Describe(FindActive(entry => ip.Equals(entry.IP)));

    public string? GetKeyBanRejectionDetail(string? publicKey) => publicKey == null ? null : Describe(FindActive(entry => entry.PlayerPublicKey == publicKey));

    /// <summary>
    ///     Bans an address, a player key, or both. An earlier ban of the same address or key is replaced.
    /// </summary>
    public async Task BanAsync(IPAddress? ip, string? publicKey, string? playerName, string? reason, string bannedBy, Perms issuerRank, TimeSpan duration)
    {
        if (ip == null && publicKey == null)
        {
            throw new ArgumentException("A ban needs an address or a key");
        }
        await loaded.Task;

        DateTimeOffset bannedAtTime = DateTimeOffset.UtcNow;
        BanEntry entry = new()
        {
            IP = ip,
            PlayerPublicKey = publicKey,
            PlayerName = playerName,
            Reason = reason ?? "",
            BannedBy = bannedBy,
            IssuerRank = issuerRank,
            BannedAtUtc = bannedAtTime,
            ExpiresAtUtc = duration == TimeSpan.Zero ? null : bannedAtTime + duration
        };
        lock (bansLock)
        {
            bans.RemoveAll(other => (ip != null && ip.Equals(other.IP)) || (publicKey != null && publicKey == other.PlayerPublicKey));
            bans.Add(entry);
        }
        SignalExpiryReschedule();
    }

    /// <summary>
    ///     Lifts the bans <paramref name="matches" /> selects, as far as someone of rank <paramref name="rank" /> may: a ban
    ///     can be lifted only by the rank that issued it or higher.
    /// </summary>
    public async Task<UnbanResult> UnbanAsync(Func<BanEntry, bool> matches, Perms rank)
    {
        await loaded.Task;
        lock (bansLock)
        {
            List<BanEntry> found = bans.Where(matches).ToList();
            if (found.Count == 0)
            {
                return new UnbanResult(0, []);
            }
            List<BanEntry> refused = found.Where(entry => !MayLift(entry, rank)).ToList();
            int removed = bans.RemoveAll(entry => found.Contains(entry) && !refused.Contains(entry));
            return new UnbanResult(removed, refused);
        }
    }

    internal static bool MayLift(BanEntry entry, Perms rank) => rank >= (entry.IssuerRank ?? UNKNOWN_ISSUER_RANK);

    public async Task<IReadOnlyList<BanEntry>> GetActiveBansAsync()
    {
        await loaded.Task;

        RemoveExpiredBans();
        lock (bansLock)
        {
            return bans.ToList();
        }
    }

    /// <summary>
    ///     Removes expired bans on startup (covers bans that lapsed while the server was offline) and then keeps sleeping
    ///     until the next ban is due to expire, waking early whenever a new ban is added.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            RemoveExpiredBans();

            TimeSpan delay = TimeSpan.FromHours(1);
            DateTimeOffset[] expiries;
            lock (bansLock)
            {
                expiries = bans.Where(entry => entry.ExpiresAtUtc.HasValue).Select(entry => entry.ExpiresAtUtc!.Value).ToArray();
            }
            if (expiries.Length > 0)
            {
                TimeSpan untilNextExpiry = expiries.Min() - DateTimeOffset.UtcNow;
                if (untilNextExpiry < delay)
                {
                    delay = untilNextExpiry > TimeSpan.FromSeconds(1) ? untilNextExpiry : TimeSpan.FromSeconds(1);
                }
            }

            try
            {
                await expiryReschedule.WaitAsync(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    async Task IEvent<ISaveState.Args>.OnEventAsync(ISaveState.Args args)
    {
        await loaded.Task;
        try
        {
            Directory.CreateDirectory(startOptions.Value.GetServerSavePath());
            List<BanEntry> snapshot;
            lock (bansLock)
            {
                snapshot = bans.ToList();
            }
            serializer.Serialize(FilePath, new BanData { Bans = snapshot });
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Could not save ban list");
        }
    }

    private BanEntry? FindActive(Func<BanEntry, bool> matches)
    {
        loaded.Task.GetAwaiter().GetResult();
        lock (bansLock)
        {
            return bans.FirstOrDefault(entry => !entry.IsExpired && matches(entry));
        }
    }

    private static string? Describe(BanEntry? entry)
    {
        if (entry == null)
        {
            return null;
        }

        StringBuilder sb = new();
        using StringWriter writer = new(sb);
        writer.NewLine = "\n";

        if (!string.IsNullOrWhiteSpace(entry.Reason))
        {
            writer.Write("Reason: ");
            writer.WriteLine(entry.Reason);
        }
        if (entry.ExpiresAtUtc.HasValue)
        {
            writer.Write("Ban expires ");
            writer.Write(entry.ExpiresAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm"));
            writer.WriteLine(" UTC");
        }
        else
        {
            writer.WriteLine("This ban is permanent");
        }
        return writer.ToString();
    }

    private void SignalExpiryReschedule()
    {
        try
        {
            expiryReschedule.Release();
        }
        catch (SemaphoreFullException)
        {
            // A rescheduling is already queued.
        }
    }

    private void RemoveExpiredBans()
    {
        lock (bansLock)
        {
            foreach (BanEntry entry in bans.Where(entry => entry.IsExpired))
            {
                logger.ZLogDebug($"A ban expired: {entry}");
            }
            bans.RemoveAll(entry => entry.IsExpired);
        }
    }

    private void Load()
    {
        try
        {
            BanData? data = serializer.Deserialize<BanData>(FilePath);
            lock (bansLock)
            {
                bans.AddRange((data?.Bans ?? []).Where(entry => entry.IP != null || entry.PlayerPublicKey != null));
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // No ban file yet (fresh save, or a save from before this feature existed).
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Could not load ban list, starting with an empty one");
        }
    }

    internal sealed record UnbanResult(int Removed, IReadOnlyList<BanEntry> Refused);

    private sealed class BanData
    {
        public List<BanEntry> Bans { get; init; } = [];
    }

    internal sealed record BanEntry
    {
        [IgnoreDataMember]
        public IPAddress? IP { get; set; }

        [JsonProperty(nameof(IP))]
        private string? JsonIP
        {
            get => IP?.ToString();
            set => IP = string.IsNullOrEmpty(value) ? null : IPAddress.Parse(value);
        }

        /// <summary>
        ///     The banned player's key, so that the ban follows them to another address. Null for a ban of a raw address.
        /// </summary>
        public string? PlayerPublicKey { get; init; }

        public string? PlayerName { get; init; }
        public string Reason { get; init; } = "";
        public string BannedBy { get; init; } = "";

        /// <summary>
        ///     The rank of whoever issued the ban; only that rank or higher may lift it. Null for a ban saved before this was kept.
        /// </summary>
        public Perms? IssuerRank { get; init; }

        public DateTimeOffset BannedAtUtc { get; init; }
        public DateTimeOffset? ExpiresAtUtc { get; init; }

        [IgnoreDataMember]
        public bool IsExpired => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= DateTimeOffset.UtcNow;

        public override string ToString() => $"[{nameof(PlayerName)}: {(string.IsNullOrEmpty(PlayerName) ? "<empty>" : PlayerName)}, {nameof(IP)}: {IP?.ToString() ?? "<none>"}, Key: {(PlayerPublicKey == null ? "<none>" : "yes")}, {nameof(BannedBy)}: {BannedBy}, {nameof(BannedAtUtc)}: {BannedAtUtc}, {nameof(Reason)}: {Reason}]";
    }
}
