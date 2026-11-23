using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Nitrox.Model.Logger;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.BedSync;
using NitroxClient.MonoBehaviours.CinematicController;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PlayerCinematicControllerCallProcessor(PlayerManager playerManager) : IClientPacketProcessor<PlayerCinematicControllerCall>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, PlayerCinematicControllerCall packet)
    {
        if (!playerManager.TryFind(packet.SessionId, out RemotePlayer remotePlayer))
        {
            return Task.CompletedTask;
        }

        // While a remote player is marked as in a cinematic, movement packets no longer restore their normal
        // animations. Every path that cannot play or finish the cinematic must therefore leave that mark cleared, or
        // the player stays frozen until they disconnect.
        if (!TryPlay(packet, remotePlayer) && !packet.StartPlaying)
        {
            EndCinematicState(remotePlayer);
        }
        return Task.CompletedTask;
    }

    /// <returns>False when the cinematic could not be started or ended on its controller.</returns>
    private static bool TryPlay(PlayerCinematicControllerCall packet, RemotePlayer remotePlayer)
    {
        if (!NitroxEntity.TryGetObjectFrom(packet.ControllerID, out GameObject gameObject))
        {
            Log.Warn($"Could not find entity with ID {packet.ControllerID} for cinematic");
            return false;
        }
        if (!remotePlayer.Body || !remotePlayer.Body.activeInHierarchy)
        {
            return false;
        }

        if (gameObject.TryGetComponent<RemoteBedController>(out var bedController))
        {
            if (packet.StartPlaying)
            {
                BeginCinematicState(remotePlayer);
                try
                {
                    bedController.StartBedAnimation(remotePlayer, packet.Key);
                }
                catch
                {
                    EndCinematicState(remotePlayer);
                    throw;
                }
                return true;
            }
            try
            {
                bedController.EndBedAnimation(remotePlayer, packet.Key);
            }
            finally
            {
                EndCinematicState(remotePlayer);
            }
            return true;
        }

        if (!gameObject.TryGetComponent<MultiplayerCinematicReference>(out var reference) || !remotePlayer.AnimationController)
        {
            return false;
        }
        if (packet.StartPlaying)
        {
            if (packet.AnimationParameters != null && packet.AnimationParameters.Count > 0)
            {
                ApplyAnimationParameters(reference, packet.Key, packet.ControllerNameHash, remotePlayer, packet.AnimationParameters);
            }
            BeginCinematicState(remotePlayer);
            try
            {
                reference.CallStartCinematicMode(packet.Key, packet.ControllerNameHash, remotePlayer);
            }
            catch (KeyNotFoundException ex)
            {
                // The controller is not registered on this client (e.g. its object has not finished loading).
                EndCinematicState(remotePlayer);
                Log.WarnOnce($"Skipped a remote cinematic that is not registered here: {ex.Message}");
                return false;
            }
            catch
            {
                EndCinematicState(remotePlayer);
                throw;
            }
            return true;
        }

        try
        {
            reference.CallCinematicModeEnd(packet.Key, packet.ControllerNameHash, remotePlayer);
        }
        catch (KeyNotFoundException ex)
        {
            Log.WarnOnce($"Could not end a remote cinematic that is not registered here: {ex.Message}");
        }
        finally
        {
            EndCinematicState(remotePlayer);
        }
        return true;
    }

    private static void BeginCinematicState(RemotePlayer remotePlayer)
    {
        remotePlayer.InCinematic = true;
        if (remotePlayer.AnimationController)
        {
            remotePlayer.AnimationController.UpdatePlayerAnimations = false;
            // The game switches this on for its own player every frame in ArmsController.Update, which does not run for
            // other players' bodies. Without it the body is moved through the cinematic in its walking pose.
            remotePlayer.AnimationController["cinematics_enabled"] = true;
        }
    }

    private static void EndCinematicState(RemotePlayer remotePlayer)
    {
        remotePlayer.InCinematic = false;
        if (remotePlayer.AnimationController)
        {
            remotePlayer.AnimationController.UpdatePlayerAnimations = true;
            remotePlayer.AnimationController["cinematics_enabled"] = false;
        }
    }

    private static void ApplyAnimationParameters(MultiplayerCinematicReference reference, string key, int controllerNameHash, RemotePlayer remotePlayer, Dictionary<string, bool> animationParameters)
    {
        if (!TryGetCinematicController(reference, key, controllerNameHash, out PlayerCinematicController cinematicController))
        {
            return;
        }
        if (cinematicController.animator != null)
        {
            foreach (KeyValuePair<string, bool> animationParameter in animationParameters)
            {
                if (animationParameter.Key == "first_use" || animationParameter.Key == "cured")
                {
                    SafeAnimator.SetBool(cinematicController.animator, animationParameter.Key, animationParameter.Value);
                }
            }
        }
        Animator componentInChildren = remotePlayer.Body.GetComponentInChildren<Animator>();
        if (!componentInChildren || !componentInChildren.gameObject.activeInHierarchy)
        {
            return;
        }
        foreach (KeyValuePair<string, bool> animationParameter2 in animationParameters)
        {
            if (animationParameter2.Key == "using_tool_first" || animationParameter2.Key == "cured")
            {
                SafeAnimator.SetBool(componentInChildren, animationParameter2.Key, animationParameter2.Value);
            }
        }
    }

    private static bool TryGetCinematicController(MultiplayerCinematicReference reference, string key, int controllerNameHash, out PlayerCinematicController cinematicController)
    {
        cinematicController = null;
        FieldInfo field = typeof(MultiplayerCinematicReference).GetField("controllerByKey", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            return false;
        }
        if (!(field.GetValue(reference) is Dictionary<string, Dictionary<int, MultiplayerCinematicController>> dictionary) || !dictionary.TryGetValue(key, out var value) || !value.TryGetValue(controllerNameHash, out var value2))
        {
            return false;
        }
        FieldInfo field2 = typeof(MultiplayerCinematicController).GetField("playerController", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field2 == null)
        {
            return false;
        }
        cinematicController = field2.GetValue(value2) as PlayerCinematicController;
        return cinematicController != null;
    }
}
