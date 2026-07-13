using System.ComponentModel;
using System.Linq;
using System.Text;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class VehicleCommand(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData,
    PlayerManager playerManager, VehicleDiagnostics diagnostics) : ICommandHandler, ICommandHandler<bool>
{
    [Description("Lists canonical vehicles, simulation owners, pilots, docks, health, and authority counters")]
    public async Task Execute(ICommandContext context)
    {
        VehicleEntity[] vehicles = entityRegistry.GetEntities<VehicleEntity>()
                                                 .OrderBy(entity => entity.TechType?.Name)
                                                 .ThenBy(entity => entity.Id.ToString())
                                                 .ToArray();
        StringBuilder output = new();
        output.AppendLine($"Vehicles: {vehicles.Length}, trace={diagnostics.TraceEnabled}, movements={diagnostics.AcceptedMovements} accepted/{diagnostics.RejectedMovements} rejected, actions={diagnostics.AcceptedActions} accepted/{diagnostics.RejectedActions} rejected");
        foreach (VehicleEntity vehicle in vehicles)
        {
            bool hasOwner = simulationOwnershipData.TryGetLock(vehicle.Id, out SimulationOwnershipData.PlayerLock owner);
            string ownerText = hasOwner ? $"{owner.Player.Name} #{owner.Player.SessionId} ({owner.LockType})" : "<none>";
            string pilots = string.Join(", ", playerManager.GetConnectedPlayers()
                                                        .Where(player => player.PlayerContext?.DrivingVehicle == vehicle.Id)
                                                        .Select(player => $"{player.Name} #{player.SessionId}"));
            float health = vehicle.Metadata switch
            {
                CyclopsMetadata cyclops => cyclops.Health,
                VehicleMetadata smallVehicle => smallVehicle.Health,
                _ => -1f
            };
            string position = vehicle.Transform == null ? "<none>" : vehicle.Transform.Position.ToString();
            output.AppendLine($"- {vehicle.TechType?.Name ?? "UNKNOWN"}: health={(health < 0f ? "unknown" : health.ToString("F1"))}, owner={ownerText}, pilot={(pilots.Length == 0 ? "<none>" : pilots)}, dock={vehicle.ParentId?.ToString() ?? "<none>"}, pos={position}, id={vehicle.Id}");
        }
        await context.ReplyAsync(output.ToString().TrimEnd());
    }

    [Description("Enables or disables detailed vehicle authority logging")]
    public async Task Execute(ICommandContext context, [Description("true to enable live trace logging")] bool traceEnabled)
    {
        diagnostics.TraceEnabled = traceEnabled;
        await context.ReplyAsync($"Vehicle live trace {(traceEnabled ? "enabled" : "disabled")}. Run 'vehicle' for a canonical snapshot.");
    }
}
