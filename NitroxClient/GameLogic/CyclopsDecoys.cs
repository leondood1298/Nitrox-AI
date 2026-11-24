using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Creature decoys launched from a Cyclops. In the game, the decoy is made by the Cyclops' launcher, rises for a moment
///     (by a distance that depends on the frame rate), stays and is removed when its lifetime is over. It is made once, in
///     the game of the player who launched it; the others get a copy made where it left the launcher and put where it came
///     to rest, removed with it.
/// </summary>
public class CyclopsDecoys
{
    /// <summary>
    ///     A copy ends with the launcher's decoy. Its own lifetime is longer, so that it only ends by itself if that news
    ///     never comes (the launching player left).
    /// </summary>
    private const float COPY_EXTRA_LIFETIME = 10f;

    private readonly IPacketSender packetSender;

    /// <summary>Decoys launched in this game, until they end; Settled once their copies were told where they rest.</summary>
    private readonly Dictionary<CyclopsDecoy, (NitroxId CyclopsId, NitroxId DecoyId, bool Settled)> launchedHere = [];

    private readonly Dictionary<NitroxId, CyclopsDecoy> copies = [];

    public CyclopsDecoys(IPacketSender packetSender)
    {
        this.packetSender = packetSender;
    }

    /// <summary>
    ///     The Cyclops' launcher made a decoy in this game (CyclopsDecoyLauncher.LaunchDecoy).
    /// </summary>
    public void OnLaunched(CyclopsDecoyLauncher launcher, CyclopsDecoy decoy)
    {
        if (!decoy || !launcher.TryGetComponentInParent(out SubRoot cyclops, true) || !cyclops.TryGetNitroxId(out NitroxId cyclopsId))
        {
            return;
        }
        NitroxId decoyId = new();
        launchedHere[decoy] = (cyclopsId, decoyId, false);
        packetSender.Send(new CyclopsDecoyUpdate(cyclopsId, decoyId, decoy.transform.position.ToDto(), CyclopsDecoyUpdate.Phase.LAUNCHED));
    }

    /// <summary>
    ///     After each CyclopsDecoy.Update: a decoy launched here that stopped rising tells its copies where it rests.
    /// </summary>
    public void OnUpdated(CyclopsDecoy decoy)
    {
        if (decoy.launch || !launchedHere.TryGetValue(decoy, out (NitroxId CyclopsId, NitroxId DecoyId, bool Settled) launched) || launched.Settled)
        {
            return;
        }
        launchedHere[decoy] = (launched.CyclopsId, launched.DecoyId, true);
        packetSender.Send(new CyclopsDecoyUpdate(launched.CyclopsId, launched.DecoyId, decoy.transform.position.ToDto(), CyclopsDecoyUpdate.Phase.SETTLED));
    }

    /// <summary>
    ///     Before CyclopsDecoy.Despawn: a decoy launched here takes its copies with it.
    /// </summary>
    public void OnEnding(CyclopsDecoy decoy)
    {
        if (launchedHere.TryGetValue(decoy, out (NitroxId CyclopsId, NitroxId DecoyId, bool Settled) launched))
        {
            launchedHere.Remove(decoy);
            packetSender.Send(new CyclopsDecoyUpdate(launched.CyclopsId, launched.DecoyId, decoy.transform.position.ToDto(), CyclopsDecoyUpdate.Phase.ENDED));
        }
    }

    /// <summary>
    ///     Another player's decoy, as told by <see cref="CyclopsDecoyUpdate" />.
    /// </summary>
    public void ApplyRemote(CyclopsDecoyUpdate update)
    {
        ForgetEndedCopies();
        Vector3 position = update.Position.ToUnity();
        switch (update.DecoyPhase)
        {
            case CyclopsDecoyUpdate.Phase.LAUNCHED:
                if (!copies.ContainsKey(update.DecoyId) && TryMakeCopy(update.CyclopsId, position, out CyclopsDecoy copy))
                {
                    copies[update.DecoyId] = copy;
                }
                break;
            case CyclopsDecoyUpdate.Phase.SETTLED:
                if (copies.TryGetValue(update.DecoyId, out CyclopsDecoy settled) && settled)
                {
                    settled.launch = false;
                    settled.transform.position = position;
                }
                break;
            case CyclopsDecoyUpdate.Phase.ENDED:
                if (copies.TryGetValue(update.DecoyId, out CyclopsDecoy ended))
                {
                    copies.Remove(update.DecoyId);
                    if (ended)
                    {
                        ended.CancelInvoke(nameof(CyclopsDecoy.Despawn));
                        ended.Despawn();
                    }
                }
                break;
        }
    }

    private static bool TryMakeCopy(NitroxId cyclopsId, Vector3 position, out CyclopsDecoy copy)
    {
        copy = null;
        if (!NitroxEntity.TryGetComponentFrom(cyclopsId, out CyclopsDecoyManager decoyManager) || !decoyManager.decoyLauncher)
        {
            Log.Debug($"[{nameof(CyclopsDecoys)}] Cyclops {cyclopsId} is not loaded here, its decoy is not shown");
            return false;
        }
        // As CyclopsDecoyLauncher.LaunchDecoy does it, but where the launching game made it
        GameObject decoyObject = Object.Instantiate(decoyManager.decoyLauncher.decoyPrefab, position, Quaternion.identity);
        copy = decoyObject.GetComponent<CyclopsDecoy>();
        if (!copy)
        {
            return false;
        }
        copy.launch = true;
        // Read in Start, which runs next frame
        copy.lifeTime += COPY_EXTRA_LIFETIME;
        return true;
    }

    private void ForgetEndedCopies()
    {
        List<NitroxId> ended = null;
        foreach (KeyValuePair<NitroxId, CyclopsDecoy> copy in copies)
        {
            if (!copy.Value)
            {
                (ended ??= []).Add(copy.Key);
            }
        }
        foreach (NitroxId decoyId in ended ?? [])
        {
            copies.Remove(decoyId);
        }
    }
}
