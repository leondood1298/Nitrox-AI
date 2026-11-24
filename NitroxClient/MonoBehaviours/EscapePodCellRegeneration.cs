using System.Collections;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     Keeps the refill of a Lifepod power cell running.
///     <para>
///     The game refills a cell in a loop that RegeneratePowerSource.Start starts as a coroutine. The Lifepod is a
///     lightmapped scene prefab: its scene loads active, EscapePod.Start spawns the three cells (they spawn OnNewBorn) and
///     their Start runs, and then LightmappedPrefabs.Update deactivates the scene's root to keep it as a prefab and
///     ActivateLoadedPrefab activates it again. The deactivation stops every coroutine in the pod, and Unity never calls
///     Start a second time, so the cells never refill. PowerSource.Start's InvokeRepeating survives the deactivation, which
///     is why the cell still reports its level.
///     </para>
///     <para>
///     Restarted on every activation, because a deactivation stops it again. The game's coroutine is stopped first, so a
///     cell never runs two. It runs in every player's game as in the unmodded one; only the player who simulates the cell
///     changes its level (PowerSource_SetPower_Patch), and that player's PowerSource.SetPower reports it.
///     </para>
/// </summary>
public sealed class EscapePodCellRegeneration : MonoBehaviour
{
    private RegeneratePowerSource regeneration;

    public static void EnsureOn(RegeneratePowerSource regeneratePowerSource)
    {
        EscapePodCellRegeneration restarter = regeneratePowerSource.gameObject.EnsureComponent<EscapePodCellRegeneration>();
        if (restarter.regeneration)
        {
            // Set up already (the pod was set up again): its loop runs, and restarting it would only reset its timer
            return;
        }
        restarter.regeneration = regeneratePowerSource;
        if (restarter.isActiveAndEnabled)
        {
            restarter.Restart();
        }
    }

    private void OnEnable()
    {
        // Not set yet when OnEnable runs inside AddComponent; EnsureOn restarts it then
        if (regeneration)
        {
            Restart();
        }
    }

    private void Restart()
    {
        StopAllCoroutines();
        StartCoroutine(RestartNextFrame());
    }

    private IEnumerator RestartNextFrame()
    {
        // A cell activated for the first time this frame still gets its own Start, which would start a second loop
        yield return null;
        if (!regeneration || !regeneration.isActiveAndEnabled)
        {
            yield break;
        }
        // The game's loop is the only coroutine this component runs
        regeneration.StopAllCoroutines();
        regeneration.StartCoroutine(regeneration.Start());
    }
}
