using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Autofac;
using HarmonyLib;
using HarmonyLib.Tools;
using NitroxClient;
using NitroxClient.Helpers;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using NitroxPatcher.Modules;
using NitroxPatcher.Patches;
using UnityEngine;

namespace NitroxPatcher;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "DIMA001:Dependency Injection container is used directly")]
internal static class Patcher
{
    /// <summary>
    ///     Dependency Injection container used by NitroxPatcher only.
    /// </summary>
    private static IContainer? container;

    private static readonly Harmony harmony = new("com.nitroxmod.harmony");
    private static bool isApplied;

    /// <summary>
    ///     Patches that could not be applied, one sentence each, for the launcher and the main menu.
    /// </summary>
    public static IReadOnlyList<string> Problems => StartupWarnings.FailedPatches;

    /// <summary>
    ///     Applies all the dynamic patches defined in Patches namespace.
    ///     Persistent patches are applied when the game initializes (i.e. before main menu).
    ///     See <see cref="Patcher.Initialize"/>.
    /// </summary>
    public static void Apply()
    {
        Validate.NotNull(container, "No patches have been discovered yet! Run Execute() first.");
        if (isApplied)
        {
            return;
        }

        int failuresBefore = StartupWarnings.FailedPatches.Count;
        foreach (IDynamicPatch patch in container.Resolve<IDynamicPatch[]>())
        {
            Log.Debug($"Applying dynamic patch {patch.GetType().Name}");
            TryPatch(patch);
        }
        isApplied = true;

        if (StartupWarnings.FailedPatches.Count > failuresBefore)
        {
            Log.InGame(StartupWarnings.DescribeFailedPatches(Language.main.Get("Nitrox_PatchesFailed"), StartupWarnings.FailedPatches.Skip(failuresBefore)));
        }
    }

    /// <summary>
    ///     Removes all the dynamic patches defined by <see cref="NitroxPatcher"/>.
    ///     <p/>
    ///     If the player starts the main menu for the first time, or returns from a (multiplayer) session, get rid of all the
    ///     patches if applicable.
    /// </summary>
    public static void Restore()
    {
        Validate.NotNull(container, "No patches have been discovered yet! Run Execute() first.");
        if (!isApplied)
        {
            return;
        }

        foreach (IDynamicPatch patch in container.Resolve<IDynamicPatch[]>())
        {
            Log.Debug($"Restoring dynamic patch {patch.GetType().Name}");
            try
            {
                patch.Restore(harmony);
            }
            catch (Exception ex)
            {
                // The others must still come off, or single player would keep running multiplayer code.
                Log.Error(ex, $"Error restoring {patch.GetType().Name}");
            }
        }

        isApplied = false;
    }

    /// <summary>
    ///     Starts Nitrox. When it throws, every patch it made has been taken out again, so the game runs vanilla.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Initialize()
    {
        if (container != null)
        {
            throw new Exception($"Patches have already been detected! Call {nameof(Apply)} or {nameof(Restore)} instead.");
        }

        Optional.ApplyHasValueCondition<UnityEngine.Object>(o => (bool)o);

        Log.Info("Registering dependencies");
        container = CreatePatchingContainer();
        try
        {
            NitroxServiceLocator.InitializeDependencyContainer(new ClientAutoFacRegistrar());
        }
        catch (ReflectionTypeLoadException ex)
        {
            Log.Error($"Failed to load one or more dependency types for Nitrox. Assembly: {ex.Types.FirstOrDefault()?.Assembly.FullName ?? "unknown"}");
            foreach (Exception loaderEx in ex.LoaderExceptions)
            {
                Log.Error(loaderEx);
            }
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while initializing and loading dependencies.");
            throw;
        }

        try
        {
            InitPatches();
            ApplyNitroxBehaviours();
        }
        catch
        {
            Multiplayer.OnBeforeMultiplayerStart -= Apply;
            Multiplayer.OnAfterMultiplayerEnd -= Restore;
            harmony.UnpatchSelf();
            throw;
        }
    }

    private static void InitPatches()
    {
        Log.Info("Patching Subnautica...");

        // Enabling this creates a log file on your desktop (why there?), showing the emitted IL instructions.
        HarmonyFileLog.Enabled = false;

        // One patch that no longer fits the game (after a game update) must not stop the others.
        foreach (IPersistentPatch patch in container.Resolve<IEnumerable<IPersistentPatch>>())
        {
            Log.Debug($"Applying persistent patch {patch.GetType().Name}");
            TryPatch(patch);
        }

        Multiplayer.OnBeforeMultiplayerStart += Apply;
        Multiplayer.OnAfterMultiplayerEnd += Restore;
        Log.Info(StartupWarnings.FailedPatches.Count == 0 ? "Completed patching" : $"Completed patching, {StartupWarnings.FailedPatches.Count} patches failed");
    }

    /// <summary>
    ///     Applies one patch whole or not at all, recording a failure for the player.
    /// </summary>
    private static void TryPatch(INitroxPatch patch)
    {
        try
        {
            patch.Patch(harmony);
        }
        catch (Exception e)
        {
            Exception innerMost = e;
            while (innerMost.InnerException != null)
            {
                innerMost = innerMost.InnerException;
            }
            Log.Error($"Error patching {patch.GetType().Name}{Environment.NewLine}{innerMost}");
            StartupWarnings.AddFailedPatch(patch.GetType().Name, innerMost.Message);
            try
            {
                patch.Restore(harmony);
            }
            catch (Exception restoreEx)
            {
                Log.Error(restoreEx, $"Error taking the half-applied {patch.GetType().Name} out again");
            }
        }
    }

    private static IContainer CreatePatchingContainer()
    {
        ContainerBuilder builder = new();
        builder.RegisterModule(new NitroxPatchesModule());
        return builder.Build();
    }

    private static void ApplyNitroxBehaviours()
    {
        Log.Info("Applying Nitrox behaviours..");

        GameObject nitroxRoot = new();
        nitroxRoot.name = "Nitrox";
        nitroxRoot.AddComponent<NitroxBootstrapper>();

        Log.Info("Behaviours applied.");
    }
}
