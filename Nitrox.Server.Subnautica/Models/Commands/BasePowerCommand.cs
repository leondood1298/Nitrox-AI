using System.ComponentModel;
using System.Linq;
using System.Text;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class BasePowerCommand(BasePowerManager basePowerManager, BasePowerDiagnostics diagnostics) : ICommandHandler, ICommandHandler<bool>
{
    [Description("Lists canonical base power sources, owners, and update counters")]
    public async Task Execute(ICommandContext context)
    {
        BasePowerManager.SourceSnapshot[] sources = basePowerManager.GetSources()
                                                                    .OrderBy(source => source.Source.ParentId?.ToString())
                                                                    .ThenBy(source => source.State.SourceType)
                                                                    .ThenBy(source => source.Source.Id.ToString())
                                                                    .ToArray();
        StringBuilder output = new();
        output.AppendLine($"Base power: {sources.Length} sources, trace={diagnostics.TraceEnabled}, accepted={diagnostics.AcceptedUpdates}, rejected={diagnostics.RejectedUpdates}");
        float totalPower = 0f;
        float totalCapacity = 0f;
        foreach (BasePowerManager.SourceSnapshot source in sources)
        {
            PowerSourceMetadata metadata = source.State;
            BasePowerSourceTypes.TryGetMaxPower(metadata.SourceType, out float maxPower);
            totalPower += metadata.Power;
            totalCapacity += maxPower;
            string ownerText = source.Simulator != null ? $"{source.Simulator.Name} #{source.Simulator.SessionId}" : "<none>";
            string fuelText = metadata.SourceType is BasePowerSourceType.BIOREACTOR or BasePowerSourceType.NUCLEAR ? $", fuel-progress={metadata.FuelConsumed:F2}" : "";
            output.AppendLine($"- {metadata.SourceType}: {metadata.Power:F2}/{maxPower:F2}{fuelText}, owner={ownerText}, id={source.Source.Id}, parent={source.Source.ParentId?.ToString() ?? "<none>"}");
        }
        output.Append($"Total source storage: {totalPower:F2}/{totalCapacity:F2}");
        await context.ReplyAsync(output.ToString());
    }

    [Description("Enables or disables detailed accepted base-power update logging")]
    public async Task Execute(ICommandContext context, [Description("true to enable live trace logging")] bool traceEnabled)
    {
        diagnostics.TraceEnabled = traceEnabled;
        await context.ReplyAsync($"Base power live trace {(traceEnabled ? "enabled" : "disabled")}. Run 'basepower' for a canonical snapshot.");
    }
}
