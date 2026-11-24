using System;
using System.Collections;
using System.Collections.Generic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Extensions;

namespace NitroxClient.GameLogic.InitialSync.Abstract;

public abstract class InitialSyncProcessor : IInitialSyncProcessor
{
    public virtual List<Func<InitialPlayerSync, IEnumerator>> Steps { get; } = new();
    public virtual HashSet<Type> DependentProcessors { get; } = new();

    /// <remarks>
    ///     A step that throws ends this processor with an <see cref="InitialSyncStepException" />, which
    ///     <see cref="NitroxClient.Communication.Packets.Processors.InitialPlayerSyncProcessor" /> turns into a failed
    ///     join. Before, the coroutine just stopped and the game waited on the loading screen forever.
    /// </remarks>
    public virtual IEnumerator Process(InitialPlayerSync packet, WaitScreen.ManualWaitItem waitScreenItem)
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            IEnumerator step;
            try
            {
                step = Steps[i](packet);
            }
            catch (Exception exception)
            {
                throw new InitialSyncStepException(GetType().Name, i, exception);
            }

            Exception? failure = null;
            yield return step.OnYieldError(exception => failure = exception);
            if (failure != null)
            {
                throw new InitialSyncStepException(GetType().Name, i, failure);
            }
            waitScreenItem.SetProgress(i + 1, Steps.Count);
            yield return null;
        }
    }

    public void AddDependency<TDependency>() where TDependency : IInitialSyncProcessor
    {
        DependentProcessors.Add(typeof(TDependency));
    }

    public void AddStep(Func<InitialPlayerSync, IEnumerator> step)
    {
        Steps.Add(step);
    }

    public void AddStep(Action<InitialPlayerSync> step)
    {
        Steps.Add(sync =>
        {
            step(sync);
            return Array.Empty<object>().GetEnumerator();
        });
    }

    public void AddStep(Func<IEnumerator> step)
    {
        Steps.Add(_ => step());
    }
}
