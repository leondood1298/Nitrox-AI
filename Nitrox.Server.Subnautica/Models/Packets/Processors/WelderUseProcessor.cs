using System.Threading.Tasks;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class WelderUseProcessor : IAuthPacketProcessor<WelderUse>
{
    public async Task Process(AuthProcessorContext context, WelderUse packet)
    {
        await context.SendToOthersAsync<WelderUse>(packet);
    }
}
