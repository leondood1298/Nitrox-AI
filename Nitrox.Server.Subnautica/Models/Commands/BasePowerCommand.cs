using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class BasePowerCommand(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData,
    BasePowerSourceAuthority authority, BasePowerDiagnostics diagnostics) : ICommandHandler, ICommandHandler<bool>
{
    /// <summary>
    ///     A client reports a source it simulates at least every thirty seconds, even when nothing changes. Three
    ///     missed reports are taken as its simulation not running.
    /// </summary>
    internal static readonly TimeSpan OWNER_SILENCE_LIMIT = TimeSpan.FromSeconds(90);

    [Description("Lists base power totals and anomalies, then every power source with its owner, last update and consumers")]
    public async Task Execute(ICommandContext context)
    {
        // The power state of a source is one immutable object, so reading it once gives a level and revision that
        // belong together, and a source losing its power state while this runs cannot break the listing.
        (Entity Entity, PowerSourceMetadata Metadata)[] sources = entityRegistry.GetAllEntities()
                                                                                .Select(entity => (Entity: entity, Metadata: entity.Metadata as PowerSourceMetadata))
                                                                                .Where(source => source.Metadata != null)
                                                                                .OrderBy(source => source.Entity.ParentId?.ToString(), StringComparer.Ordinal)
                                                                                .ThenBy(source => source.Metadata.SourceType)
                                                                                .ThenBy(source => source.Entity.Id.ToString(), StringComparer.Ordinal)
                                                                                .ToArray();
        IReadOnlyDictionary<NitroxId, BasePowerSourceDiagnostics> recorded = diagnostics.GetSources();
        BasePowerDiagnosticTotals totals = diagnostics.GetTotals();
        List<string> anomalies = [];
        List<string> lines = [];
        float totalPower = 0f;
        float totalCapacity = 0f;
        int owned = 0;
        foreach ((Entity source, PowerSourceMetadata metadata) in sources)
        {
            totalPower += metadata.Power;
            totalCapacity += metadata.MaxPower;
            bool hasOwner = simulationOwnershipData.TryGetLock(source.Id, out SimulationOwnershipData.PlayerLock owner);
            recorded.TryGetValue(source.Id, out BasePowerSourceDiagnostics record);
            string name = $"{metadata.SourceType} {DiagnosticLineFormat.ShortId(source.Id, true)}";
            string ownerText = "<none>";
            if (hasOwner)
            {
                owned++;
                ownerText = $"{owner.Player.Name} #{owner.Player.SessionId} ({owner.Player.GameMode})";
                if (record.LastOwner != owner.Player.SessionId)
                {
                    anomalies.Add($"{name} is simulated by {owner.Player.Name} #{owner.Player.SessionId}, who has not reported it yet: until then its level is frozen (still loading, or the source is missing there and is handed back once someone draws from it)");
                }
                else if (record.SinceLastUpdate is { } silence && silence > OWNER_SILENCE_LIMIT)
                {
                    anomalies.Add(string.Create(CultureInfo.InvariantCulture,
                        $"{name} is simulated by {owner.Player.Name} #{owner.Player.SessionId}, who last reported it {silence.TotalSeconds:F0}s ago (expected at least every 30s): its level is frozen"));
                }
            }
            else
            {
                anomalies.Add($"{name} has no simulating player: it generates nothing, and consumption is applied to the stored level by the server");
            }
            if (record.RevisionRegressions > 0)
            {
                anomalies.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{name} had its revision go backwards {record.RevisionRegressions} time(s) despite the revision floor: clients that hold a higher revision ignore its updates"));
            }
            if (record.RejectedUpdates > 0)
            {
                anomalies.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{name} had {record.RejectedUpdates} update(s) rejected, last reason: {record.LastRejection ?? DiagnosticLineFormat.NOT_APPLICABLE}"));
            }

            string fuelText = metadata.SourceType is BasePowerSourceType.BIOREACTOR or BasePowerSourceType.NUCLEAR
                ? string.Create(CultureInfo.InvariantCulture, $", fuel-progress={metadata.FuelConsumed:F2}")
                : "";
            string updateText = record.SinceLastUpdate is { } sinceLastUpdate
                ? string.Create(CultureInfo.InvariantCulture, $"last update {sinceLastUpdate.TotalSeconds:F1}s ago by #{DiagnosticLineFormat.Session(record.LastOwner)}")
                : "no update since server start";
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"- {name}: {metadata.Power:F2}/{metadata.MaxPower:F2}{fuelText}, rev={metadata.Revision}, seq={authority.GetLastClientSequence(source.Id)}, owner={ownerText}, {updateText}, updates={record.AcceptedUpdates} rejected={record.RejectedUpdates} handed back={record.HandBacks}{FormatConsumers(record.Consumers)}, id={source.Id}, parent={source.ParentId?.ToString() ?? "<none>"}"));
        }

        StringBuilder output = new();
        output.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Base power ep={diagnostics.Epoch}: sources={sources.Length} (simulated={owned}, unowned={sources.Length - owned}), stored={totalPower:F2}/{totalCapacity:F2}, updates accepted={totals.AcceptedUpdates} rejected={totals.RejectedUpdates}, consumption forwarded={totals.ConsumptionForwarded} ({totals.ConsumptionForwardedAmount:F2}) applied by server={totals.ConsumptionAppliedByServer} ({totals.ConsumptionAppliedByServerAmount:F2}) rejected={totals.ConsumptionRejected}, revision regressions={totals.RevisionRegressions}, trace={(diagnostics.TraceEnabled ? "on" : "off")}, diagnostics failures={diagnostics.OwnFailures}"));
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Anomalies ({anomalies.Count}):"));
        foreach (string anomaly in anomalies)
        {
            output.AppendLine($" ! {anomaly}");
        }
        output.AppendLine("Sources:");
        foreach (string line in lines)
        {
            output.AppendLine(line);
        }
        output.Append(string.Create(CultureInfo.InvariantCulture, $"Total source storage: {totalPower:F2}/{totalCapacity:F2}"));
        await context.ReplyAsync(output.ToString());
    }

    [Description("Enables or disables detailed accepted base-power update logging")]
    public async Task Execute(ICommandContext context, [Description("true to enable live trace logging")] bool traceEnabled)
    {
        diagnostics.TraceEnabled = traceEnabled;
        await context.ReplyAsync($"Base power live trace {(traceEnabled ? "enabled" : "disabled")}. Run 'basepower' for a canonical snapshot.");
    }

    /// <summary>
    ///     What each player reported as consumed from a source it does not simulate. Two players reporting the same
    ///     consumer, or a consumer that reports nothing, shows up here.
    /// </summary>
    private static string FormatConsumers(IReadOnlyList<BasePowerConsumerDiagnostics>? consumers)
    {
        if (consumers == null || consumers.Count == 0)
        {
            return ", consumed by others: nothing reported";
        }
        return $", consumed by others: {string.Join("; ", consumers.Select(consumer => string.Create(CultureInfo.InvariantCulture,
            $"#{DiagnosticLineFormat.Session(consumer.Consumer)} forwarded {consumer.Forwarded} ({consumer.ForwardedAmount:F2}) server-applied {consumer.ServerApplied} ({consumer.ServerAppliedAmount:F2}) rejected {consumer.Rejected}")))}";
    }
}
