using System.ComponentModel;
using System.Linq;
using System.Text;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class BasePowerCommand(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData,
    BasePowerSourceAuthority authority, BasePowerDiagnostics diagnostics) : ICommandHandler, ICommandHandler<bool>
{
    [Description("Lists canonical base power sources, owners, revisions, and update counters")]
    public async Task Execute(ICommandContext context)
    {
        Entity[] sources = entityRegistry.GetAllEntities()
                                         .Where(entity => entity.Metadata is PowerSourceMetadata)
                                         .OrderBy(entity => entity.ParentId?.ToString())
                                         .ThenBy(entity => ((PowerSourceMetadata)entity.Metadata).SourceType)
                                         .ThenBy(entity => entity.Id.ToString())
                                         .ToArray();
        StringBuilder output = new();
        output.AppendLine($"Base power: {sources.Length} sources, trace={diagnostics.TraceEnabled}, accepted={diagnostics.AcceptedUpdates}, rejected={diagnostics.RejectedUpdates}");
        float totalPower = 0f;
        float totalCapacity = 0f;
        foreach (Entity source in sources)
        {
            PowerSourceMetadata metadata = (PowerSourceMetadata)source.Metadata;
            totalPower += metadata.Power;
            totalCapacity += metadata.MaxPower;
            bool hasOwner = simulationOwnershipData.TryGetLock(source.Id, out SimulationOwnershipData.PlayerLock owner);
            string ownerText = hasOwner ? $"{owner.Player.Name} #{owner.Player.SessionId}" : "<none>";
            string fuelText = metadata.SourceType is BasePowerSourceType.BIOREACTOR or BasePowerSourceType.NUCLEAR ? $", fuel-progress={metadata.FuelConsumed:F2}" : "";
            output.AppendLine($"- {metadata.SourceType}: {metadata.Power:F2}/{metadata.MaxPower:F2}{fuelText}, rev={metadata.Revision}, seq={authority.GetLastClientSequence(source.Id)}, owner={ownerText}, id={source.Id}, parent={source.ParentId?.ToString() ?? "<none>"}");
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
