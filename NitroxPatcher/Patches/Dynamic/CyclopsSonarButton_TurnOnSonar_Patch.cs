using System;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class CyclopsSonarButton_TurnOnSonar_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsSonarButton t) => t.TurnOnSonar());

    public static void Postfix(CyclopsSonarButton __instance)
    {
        if (!PacketSuppressor<EntityMetadataUpdate>.IsSuppressed && __instance.subRoot.TryGetIdOrWarn(out NitroxId nitroxId))
        {
            Resolve<Cyclops>().BroadcastMetadataChange(nitroxId);
        }
    }
}
