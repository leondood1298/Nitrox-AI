using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class EscapePodMetadataAuthority
{
    public static EscapePodMetadata Merge(EscapePodMetadata? current, EscapePodMetadata requested) =>
        new(current?.PodRepaired == true || requested.PodRepaired,
            current?.RadioRepaired == true || requested.RadioRepaired);
}
