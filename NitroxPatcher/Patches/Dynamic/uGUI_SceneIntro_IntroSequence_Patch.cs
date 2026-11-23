using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class uGUI_SceneIntro_IntroSequence_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo targetMethod = AccessTools.EnumeratorMoveNext(Reflect.Method((uGUI_SceneIntro si) => si.IntroSequence()));

    private static RemotePlayer? partner;
    private static bool callbackRun;
    private static bool packetSend;

    public static bool IsWaitingForPartner { get; private set; }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
               // Replace LargeWorldStreamer.Main.IsWorldSettled() check with IsWorldSettledAndInitialSyncCompleted()
               .MatchStartForward(
                   new CodeMatch(OpCodes.Ldsfld, Reflect.Field(() => LargeWorldStreamer.main)),
                   new CodeMatch(OpCodes.Callvirt, Reflect.Method((LargeWorldStreamer lws) => lws.IsWorldSettled()))
               )
               .SetAndAdvance(OpCodes.Call, Reflect.Method(() => IsWorldSettledAndInitialSyncCompleted()))
               .RemoveInstruction()
               // Replace GameInput.AnyKeyDown check with AnyKeyDownOrSubstitute()
               .MatchEndForward(
                   new CodeMatch(OpCodes.Call, Reflect.Property(() => GameInput.AnyKeyDown).GetMethod)
               )
               .SetOperandAndAdvance(Reflect.Method(() => AnyKeyDownOrSubstitute()))
               // Insert custom check if cinematic should be started => waiting for other player & enable skip functionality
               .MatchEndForward(
                   new CodeMatch(OpCodes.Ldfld, Reflect.Field((uGUI_SceneIntro si) => si.moveNext)),
                   new CodeMatch(OpCodes.Brfalse))
               .Insert(
                   new CodeInstruction(OpCodes.Ldloc_1),
                   new CodeInstruction(OpCodes.Call, Reflect.Method(() => IsRemoteCinematicReady(default))),
                   new CodeInstruction(OpCodes.And)
               )
               // Run our prepare code when cinematic starts
               .MatchEndForward(
                   new CodeMatch(OpCodes.Ldsfld, Reflect.Field(() => EscapePod.main)),
                   new CodeMatch(OpCodes.Callvirt, Reflect.Method(() => EscapePod.main.TriggerIntroCinematic()))
               )
               .Advance(1)
               .Insert(
                   new CodeInstruction(OpCodes.Call, Reflect.Method(() => StartRemoteCinematic()))
               )
               // Disable cinematic skip when cinematic already started in duo mode
               .MatchStartForward(
                   new CodeMatch(OpCodes.Call, Reflect.Property(() => Time.time).GetMethod)
               )
               .SetOperandAndAdvance(Reflect.Method(() => GetSkipTime()))
               .Advance(1)
               .Insert(
                   new CodeInstruction(OpCodes.Ldloc_1),
                   new CodeInstruction(OpCodes.Ldfld, Reflect.Field((uGUI_SceneIntro si) => si.skipText)),
                   new CodeInstruction(OpCodes.Ldc_R4, 0f),
                   new CodeInstruction(OpCodes.Call, Reflect.Method(() => TMProExtensions.SetAlpha(default, default(float))))
               )
               // Replace intro text
               .MatchEndForward(
                   new CodeMatch(OpCodes.Ldstr, "IntroUWEPresents")
               )
               .SetOperandAndAdvance("Nitrox_IntroUWEPresents")
               // Run our cleanup code when cinematic ends
               .MatchEndForward(
                   new CodeMatch(OpCodes.Ldloc_1),
                   new CodeMatch(OpCodes.Ldc_I4_0),
                   new CodeMatch(OpCodes.Call, Reflect.Method((uGUI_SceneIntro si) => si.Stop(default)))
               )
               .Advance(1)
               .Insert(
                   new CodeInstruction(OpCodes.Call, Reflect.Method(() => EndRemoteCinematic()))
               )
               .InstructionEnumeration();
    }

    public static void EnqueueStartCinematic(uGUI_SceneIntro uGuiSceneIntro)
    {
        IsWaitingForPartner = false;
        uGuiSceneIntro.skipHintStartTime = -1;
        uGuiSceneIntro.moveNext = false;
        uGuiSceneIntro.mainText.FadeOut(0.2f, uGuiSceneIntro.Callback);
        callbackRun = true;
    }

    public static void SkipLocalCinematic(uGUI_SceneIntro uGuiSceneIntro, bool wasNewPlayer)
    {
        LiveMixin radioLiveMixin = EscapePod.main.radioSpawner.spawnedObj.GetComponent<Radio>().liveMixin;
        float radioHealthBefore = radioLiveMixin.health;
        float playerHealthBefore = Player.main.liveMixin.health;

        uGuiSceneIntro.Stop(true);

        if (!wasNewPlayer)
        {
            // EscapePod.StopIntroCinematic(true), called by uGUI_SceneIntro.Stop(true), damages the radio and takes 20
            // health from a player at full health: the crash of a new arrival, not of a player who comes back.
            radioLiveMixin.health = radioHealthBefore;
            Player.main.liveMixin.health = playerHealthBefore;
        }

        if (radioLiveMixin.IsFullHealth())
        {
            Object.Destroy(radioLiveMixin.loopingDamageEffectObj);
        }

        Transform introFireHolder = EscapePod.main.transform.Find("Intro");
        if (introFireHolder) // Can be null if called very early
        {
            if (introFireHolder.TryGetComponentInChildren(out NitroxEntity? fireEntity))
            {
                Resolve<Entities>().RemoveEntity(fireEntity.Id);
            }

            introFireHolder.GetComponentInChildren<FMOD_CustomEmitter>(true).ReleaseEvent(); // Not releasing it before destroying results in infinite unstoppable pain
            Object.DestroyImmediate(introFireHolder.gameObject); // Like in Fire.Extinguished() but without delay
        }

        // From uGUI_SceneIntro.IntroSequence() after "if (XRSettings.enabled && VROptions.skipIntro)"
        if (UnityObjectExtensions.TryFind("fire_extinguisher_01_tp", out GameObject gameObject1))
        {
            Object.Destroy(gameObject1);
        }
        if (UnityObjectExtensions.TryFind("IntroFireExtinugisherPickup", out GameObject gameObject2))
        {
            Object.Destroy(gameObject2);
        }

        // Stop(true) also showed the pod's damaged effects and set its emergency lighting.
        Resolve<EscapePodRepairState>().ApplyAfterIntro();

        Resolve<PlayerCinematics>().SetLocalIntroCinematicMode(IntroCinematicMode.COMPLETED);
    }

    // ReSharper disable once UnusedMethodReturnValue.Local
    private static bool IsWorldSettledAndInitialSyncCompleted()
    {
        bool ready = LargeWorldStreamer.main.IsWorldSettled() && Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        if (!ready)
        {
            // The intro of this session has not begun. What is left of the intro of an earlier session in the same game
            // process must go: with packetSend still set, a new player would never tell the server that they wait, and
            // would sit in front of a black screen.
            partner = null;
            callbackRun = false;
            packetSend = false;
            IsWaitingForPartner = false;
        }
        return ready;
    }

    // ReSharper disable once UnusedMethodReturnValue.Local
    private static bool AnyKeyDownOrSubstitute()
    {
        return GameInput.AnyKeyDown ||
               !NitroxEnvironment.IsReleaseMode ||
               Resolve<LocalPlayer>().IntroCinematicMode == IntroCinematicMode.COMPLETED;
    }

    // ReSharper disable once UnusedMethodReturnValue.Local
    private static float GetSkipTime()
    {
        // Return Time.time when starting solo and disable skip button when staring duo
        return Resolve<LocalPlayer>().IntroCinematicMode == IntroCinematicMode.SINGLEPLAYER ? Time.time : -1f;
    }

    // ReSharper disable once UnusedMethodReturnValue.Local
    private static bool IsRemoteCinematicReady(uGUI_SceneIntro uGuiSceneIntro)
    {
        if (callbackRun)
        {
            return true;
        }

        if (Resolve<LocalPlayer>().IntroCinematicMode == IntroCinematicMode.COMPLETED)
        {
            SkipLocalCinematic(uGuiSceneIntro, false);
            return false;
        }

        // Skipping intro if creative like in normal SN or in debug configuration
        if (ShouldSkipIntroForDebug() ||
            GameModeUtils.currentGameMode.HasFlag(GameModeOption.Creative))
        {
            SkipLocalCinematic(uGuiSceneIntro, true);
            return false;
        }

        if (!packetSend)
        {
            uGuiSceneIntro.skipHintStartTime = Time.time;
            uGuiSceneIntro.mainText.SetText(Language.main.GetFormat("Nitrox_IntroWaitingPartner", GameInput.FormatButton(GameInput.Button.UIMenu)));
            uGuiSceneIntro.mainText.SetState(true);

            Resolve<PlayerCinematics>().SetLocalIntroCinematicMode(IntroCinematicMode.WAITING);
            packetSend = true;
            IsWaitingForPartner = true;
            return false;
        }

        SessionId? opPartnerId = Resolve<PlayerCinematics>().IntroCinematicPartnerId;

        // A partner who chose to play alone when the server paired it is not taken; the server lets this player wait again
        if (Resolve<LocalPlayer>().IntroCinematicMode == IntroCinematicMode.START &&
            opPartnerId.HasValue && Resolve<PlayerManager>().TryFind(opPartnerId.Value, out RemotePlayer newPartner) &&
            newPartner.PlayerContext.IntroCinematicMode != IntroCinematicMode.SINGLEPLAYER)
        {
            partner = newPartner;
            EnqueueStartCinematic(uGuiSceneIntro);
            Resolve<PlayerCinematics>().SetLocalIntroCinematicMode(IntroCinematicMode.PLAYING);
            Resolve<PlayerCinematics>().IntroCinematicPartnerId = null;
        }

        return false;
    }

    private static bool ShouldSkipIntroForDebug() =>
        !NitroxEnvironment.IsReleaseMode &&
        !NitroxEnvironment.CommandLineArgs.Any(x => x.Equals("--noskipintro", StringComparison.OrdinalIgnoreCase));

    private static void StartRemoteCinematic()
    {
        if (IsPartnerValid()) // Is null when RunCinematicSingleplayer() is called
        {
            Resolve<PlayerCinematics>().StartIntroWithPartner(partner);
        }
    }

    private static void EndRemoteCinematic()
    {
        partner = null;
        Resolve<PlayerCinematics>().EndIntroWithPartner();

        // Stop(false) damaged the radio. While the intro fire still burns, IntroLifepodDirector.ConcludeIntroSequence
        // will set the emergency lighting once it is put out; the stored state is applied after that instead
        // (IntroLifepodDirector_ConcludeIntroSequence_Patch).
        if (!IntroLifepodDirector.IsActive)
        {
            Resolve<EscapePodRepairState>().ApplyAfterIntro();
        }

        Resolve<PlayerCinematics>().SetLocalIntroCinematicMode(IntroCinematicMode.COMPLETED);
    }

    [MemberNotNullWhen(true, nameof(partner))]
    private static bool IsPartnerValid() => partner != null && Resolve<PlayerManager>().Find(partner.SessionId).HasValue;
}
