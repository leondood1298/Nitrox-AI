using System.Collections.Generic;
using Nitrox.Model.GameLogic.PlayerAnimation;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     Tells other players when the local player opens the PDA, uses a tool, jumps and so on. The game works these out
///     every frame in several places (<see cref="ArmsController.Update" />, <see cref="ArmsController.SetUsingPda" />, the
///     tools themselves), so the outcome is read from the animator instead of patching each place.
/// </summary>
public class PlayerAnimatorStateTracker : MonoBehaviour
{
    private readonly Dictionary<AnimChangeType, bool> lastStates = [];
    private readonly List<KeyValuePair<AnimChangeType, int>> trackedSwitches = [];
    private Animator trackedAnimator;
    private LocalPlayer localPlayer;

    public void Awake()
    {
        localPlayer = this.Resolve<LocalPlayer>();
        enabled = false;

        Multiplayer.OnLoadingComplete += Enable;
    }

    public void OnDestroy()
    {
        Multiplayer.OnLoadingComplete -= Enable;
    }

    private void Enable()
    {
        enabled = true;
    }

    /// <summary>
    ///     Runs after every Update, so that the values the game set this frame are the ones compared.
    /// </summary>
    public void LateUpdate()
    {
        Player player = Player.main;
        if (!player || !player.armsController)
        {
            return;
        }
        Animator animator = player.armsController.animator;
        if (!animator || !animator.isActiveAndEnabled)
        {
            return;
        }
        if (animator != trackedAnimator && !TryTrack(animator))
        {
            return;
        }

        foreach (KeyValuePair<AnimChangeType, int> tracked in trackedSwitches)
        {
            bool isOn = animator.GetBool(tracked.Value);
            lastStates.TryGetValue(tracked.Key, out bool wasOn);
            if (isOn == wasOn)
            {
                continue;
            }
            lastStates[tracked.Key] = isOn;
            localPlayer.AnimationChange(tracked.Key, isOn ? AnimChangeState.ON : AnimChangeState.OFF);
        }
    }

    /// <summary>
    ///     Looks up which of the switches this animator has. Reading one it does not have logs a warning every frame.
    /// </summary>
    private bool TryTrack(Animator animator)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        if (parameters.Length == 0)
        {
            // Not initialised yet.
            return false;
        }

        trackedSwitches.Clear();
        foreach (KeyValuePair<AnimChangeType, string> parameter in PlayerAnimationParameters.Switches)
        {
            foreach (AnimatorControllerParameter existing in parameters)
            {
                if (existing.type == AnimatorControllerParameterType.Bool && existing.name == parameter.Value)
                {
                    trackedSwitches.Add(new(parameter.Key, existing.nameHash));
                    break;
                }
            }
        }
        if (trackedSwitches.Count < PlayerAnimationParameters.Switches.Count)
        {
            Log.Warn($"[{nameof(PlayerAnimatorStateTracker)}] The player animator has only {trackedSwitches.Count} of the {PlayerAnimationParameters.Switches.Count} switches that are synced");
        }
        trackedAnimator = animator;
        return true;
    }
}
