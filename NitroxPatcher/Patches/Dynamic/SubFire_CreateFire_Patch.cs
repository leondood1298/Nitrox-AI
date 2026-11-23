using System;
using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Hook onto <see cref="SubFire.CreateFire(SubFire.RoomFire)" />. We do this on the fire creation method because
///     unlike <see cref="SubRoot.OnTakeDamage(DamageInfo)" />, fires
///     can be created outside of <see cref="SubFire.OnTakeDamage(DamageInfo)" />
/// </summary>
public sealed partial class SubFire_CreateFire_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SubFire t) => t.CreateFire(default(SubFire.RoomFire)));

    public static bool Prefix(SubFire __instance, SubFire.RoomFire startInRoom, out bool __state)
    {
        __state = __instance.subRoot.TryGetIdOrWarn(out NitroxId id) &&
                  Resolve<SimulationOwnership>().HasAnyLockType(id);

        // Block any new fires if this player is not the owner
        return __state;
    }

    public static void Postfix(SubFire __instance, SubFire.RoomFire startInRoom, bool __state)
    {
        // CreateFire picks a random free node in the room and asks the node's PrefabSpawnBase to spawn the fire. That
        // goes through a SpawnRequest that completes later, so the fire object does not exist yet when this runs.
        if (__state)
        {
            Resolve<Fires>().AnnounceNewFire(__instance, startInRoom);
        }
    }
}
