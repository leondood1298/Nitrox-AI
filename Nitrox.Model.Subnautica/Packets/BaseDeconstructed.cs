using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The last piece of a base was deconstructed: the base becomes a ghost. Ordered like the other changes of a base, so
///     that it cannot take away a piece another player finished at the same moment.
/// </summary>
[Serializable]
public sealed class BaseDeconstructed : OrderedBuildPacket
{
    public NitroxId FormerBaseId { get; }
    public GhostEntity ReplacerGhost { get; }

    public BaseDeconstructed(NitroxId formerBaseId, GhostEntity replacerGhost, int operationId) : base(operationId)
    {
        FormerBaseId = formerBaseId;
        ReplacerGhost = replacerGhost;
    }
}
