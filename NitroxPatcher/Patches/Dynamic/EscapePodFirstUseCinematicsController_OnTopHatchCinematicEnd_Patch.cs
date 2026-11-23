using System;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using NitroxClient.GameLogic.Spawning.Metadata;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class EscapePodFirstUseCinematicsController_OnTopHatchCinematicEnd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((EscapePodFirstUseCinematicsController t) => t.OnTopHatchCinematicEnd((CinematicModeEventData)null));

    public static void Postfix(EscapePodFirstUseCinematicsController __instance)
    {
        if (__instance.escapePod.TryGetIdOrWarn(out NitroxId nitroxId))
        {
            EntityMetadata value = Resolve<EntityMetadataManager>().Extract(__instance.escapePod.gameObject).Value;
            Resolve<IPacketSender>().Send(new EntityMetadataUpdate(nitroxId, value));
        }
    }
}
