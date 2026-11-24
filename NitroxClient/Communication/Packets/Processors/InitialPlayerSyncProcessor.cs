using System;
using System.Collections;
using System.Collections.Generic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic.InitialSync.Abstract;
using NitroxClient.Extensions;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.Gui.Modals;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class InitialPlayerSyncProcessor(IEnumerable<IInitialSyncProcessor> processors, IMultiplayerSession session) : IClientPacketProcessor<InitialPlayerSync>
{
    private readonly IMultiplayerSession session = session;
    private readonly HashSet<Type> alreadyRan = [];
    private Exception? failure;
    private readonly HashSet<IInitialSyncProcessor> processors = processors.ToSet();

    private int cumulativeProcessorsRan;

    private WaitScreen.ManualWaitItem loadingMultiplayerWaitItem;
    private InitialPlayerSync packet;
    private int processorsRanLastCycle;
    private WaitScreen.ManualWaitItem subWaitScreenItem;

    public Task Process(ClientProcessorContext context, InitialPlayerSync packet)
    {
        this.packet = packet;

        loadingMultiplayerWaitItem = WaitScreen.Add(Language.main.Get("Nitrox_SyncingWorld"));
        Log.InGame(Language.main.Get("Nitrox_SyncingWorld"));

        cumulativeProcessorsRan = 0;
        Multiplayer.Main.StartCoroutine(ProcessInitialSyncPacket(context));
        return Task.CompletedTask;
    }

    private IEnumerator ProcessInitialSyncPacket(ClientProcessorContext context)
    {
        bool moreProcessorsToRun;
        do
        {
            yield return Multiplayer.Main.StartCoroutine(RunPendingProcessors());
            if (failure != null)
            {
                FailJoin(failure);
                yield break;
            }

            moreProcessorsToRun = alreadyRan.Count < processors.Count;
            if (moreProcessorsToRun && processorsRanLastCycle == 0)
            {
                FailJoin(new InitialSyncStepException(GetRemainingProcessorsText(), 0, new Exception("Detected circular dependencies in initial packet sync")));
                yield break;
            }
        } while (moreProcessorsToRun);

        WaitScreen.Remove(loadingMultiplayerWaitItem);

        // What changed in the world while the snapshot was applied comes on top of it, before the others are told this
        // player is in. Replayed before InitialSyncCompleted is set, so that processors with their own loading-screen
        // handling take the same branch as when these packets used to be applied during loading.
        Multiplayer.Main.ReplayHeldPackets();
        Multiplayer.Main.InitialSyncCompleted = true;

        // When the player finishes loading, we can take back his invincibility
        Player.main.liveMixin.invincible = false;
        Player.main.UnfreezeStats();

        context.Send(new PlayerSyncFinished());
    }

    private IEnumerator RunPendingProcessors()
    {
        processorsRanLastCycle = 0;

        foreach (IInitialSyncProcessor processor in processors)
        {
            if (IsWaitingToRun(processor.GetType()) && HasDependenciesSatisfied(processor))
            {
                loadingMultiplayerWaitItem.SetProgress(cumulativeProcessorsRan, processors.Count);

                alreadyRan.Add(processor.GetType());
                processorsRanLastCycle++;
                cumulativeProcessorsRan++;

                Log.Info($"Running {processor.GetType()}");
                subWaitScreenItem = WaitScreen.Add($"Running {processor.GetType().Name}");
                yield return Multiplayer.Main.StartCoroutine(processor.Process(packet, subWaitScreenItem).OnYieldError(exception => failure = exception));
                WaitScreen.Remove(subWaitScreenItem);
                if (failure != null)
                {
                    yield break;
                }
            }
        }
    }

    /// <summary>
    ///     The world could not be set up from the snapshot. Rather than leave the player on the loading screen for good,
    ///     tell them and disconnect; the server then drops them from the join queue.
    /// </summary>
    private void FailJoin(Exception exception)
    {
        Log.Error(exception, "Joining failed while applying the initial sync");
        string step = exception is InitialSyncStepException stepException ? stepException.Step : exception.GetType().Name;
        session.Disconnect();
        Modal.Get<KickedModal>().Show(Language.main.Get("Nitrox_JoinFailed").Replace("{STEP}", step));
    }

    private bool HasDependenciesSatisfied(IInitialSyncProcessor processor)
    {
        foreach (Type dependentType in processor.DependentProcessors)
        {
            if (IsWaitingToRun(dependentType))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsWaitingToRun(Type processor)
    {
        return !alreadyRan.Contains(processor);
    }

    private string GetRemainingProcessorsText()
    {
        string remaining = "";

        foreach (IInitialSyncProcessor processor in processors)
        {
            if (IsWaitingToRun(processor.GetType()))
            {
                remaining += $" {processor.GetType()}";
            }
        }

        return remaining;
    }
}
