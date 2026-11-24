using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.GameLogic.FMOD;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Who gets a sound with a position. The volume is not touched here: the receiving game fades it by its own distance,
///     so a sound is made quieter in one place only.
/// </summary>
internal static class SoundRange
{
    public static bool Reaches(SoundData sound, NitroxVector3 soundPosition, Optional<NitroxId> senderSubRootId, NitroxVector3 listenerPosition, Optional<NitroxId> listenerSubRootId)
    {
        if (!sound.IsGlobal && !listenerSubRootId.Equals(senderSubRootId))
        {
            return false;
        }
        return NitroxVector3.Distance(listenerPosition, soundPosition) <= sound.Radius;
    }
}
