using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.FMOD;
using Nitrox.Model.GameLogic.FMOD;
using NitroxClient.GameLogic.Spawning.Bases;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.Cyclops;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using NitroxPatcher.Helper;
using NitroxPatcher.PatternMatching;
using UnityEngine;
using static System.Reflection.Emit.OpCodes;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Builder_TryPlace_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => Builder.TryPlace());

    private const string PLACE_SOUND_PATH = "event:/tools/builder/place";

    public static readonly InstructionsPattern AddInstructionPattern1 = new()
    {
        Ldloc_0,
        Ldc_I4_0,
        Ldc_I4_1,
        new() { OpCode = Callvirt, Operand = new(nameof(Constructable), nameof(Constructable.SetState)) },
        { Pop, "Insert1" }
    };

    public static readonly List<CodeInstruction> InstructionsToAdd1 = new()
    {
        new(Ldloc_0),
        new(Call, Reflect.Method(() => GhostCreated(default)))
    };

    public static readonly InstructionsPattern AddInstructionPattern2 = new()
    {
        Ldloc_S,
        Ldloc_3,
        Ldloc_S,
        Or,
        { new() { OpCode = Callvirt, Operand = new(nameof(Constructable), nameof(Constructable.SetIsInside)) }, "Insert2" }
    };

    public static readonly List<CodeInstruction> InstructionsToAdd2 = new()
    {
        TARGET_METHOD.Ldloc<Constructable>(),
        new(Call, Reflect.Method(() => GhostCreated(default)))
    };

    public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions) =>
        instructions.Transform(AddInstructionPattern1, (label, instruction) =>
        {
            if (label.Equals("Insert1"))
            {
                return InstructionsToAdd1;
            }
            return null;
        }).Transform(AddInstructionPattern2, (label, instruction) =>
        {
            if (label.Equals("Insert2"))
            {
                return InstructionsToAdd2;
            }
            return null;
        });

    public static void GhostCreated(Constructable constructable)
    {
        GameObject ghostObject = constructable.gameObject;

        NitroxId parentId = null;
        if (ghostObject.TryGetComponentInParent(out SubRoot subRoot, true) && (subRoot.isBase || subRoot.isCyclops) &&
            subRoot.TryGetNitroxId(out NitroxId entityId))
        {
            parentId = entityId;
        }

        // The game plays its placing sound through FMODUnity's RuntimeManager, which is not sent like its other one-shots
        if (Resolve<FMODWhitelist>().IsWhitelisted(PLACE_SOUND_PATH))
        {
            Resolve<FMODSystem>().SendAssetPlay(PLACE_SOUND_PATH, ghostObject.transform.position.ToDto(), 1f);
        }

        // Assign a NitroxId to the ghost now
        NitroxId ghostId = new();
        NitroxEntity.SetNewId(ghostObject, ghostId);
        if (constructable is ConstructableBase constructableBase)
        {
            GhostEntity ghost = GhostEntitySpawner.From(constructableBase);
            ghost.Id = ghostId;
            ghost.ParentId = parentId;
            Resolve<IPacketSender>().Send(new PlaceGhost(ghost));
        }
        else
        {
            ModuleEntitySpawner.MoveToGlobalRoot(ghostObject);
            
            ModuleEntity module = ModuleEntitySpawner.From(constructable, Resolve<EntityMetadataManager>());
            module.Id = ghostId;
            module.ParentId = parentId;
            Resolve<IPacketSender>().Send(new PlaceModule(module));

            if (constructable.transform.parent && constructable.transform.parent.TryGetComponent(out NitroxCyclops nitroxCyclops) && nitroxCyclops.Virtual)
            {
                nitroxCyclops.Virtual.ReplicateConstructable(constructable);
            }
        }
    }
}
