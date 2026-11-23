using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <remarks>
///     The prison cell spawns again whenever a player comes near, and with it this metadata. The game's methods for powering
///     and hatching play sounds, trigger story goals and start the hatching (whose patch spawns new babies for the
///     simulating player), so a state that is only being restored is set directly. The hatching is only played for a player
///     who watched the incubator wait for it.
/// </remarks>
public class IncubatorMetadataProcessor : EntityMetadataProcessor<IncubatorMetadata>
{
    // From Incubator.SetPowered and Incubator.Start
    private const float LIGHT_INTENSITY_PER_EMISSIVE = 1.85f;

    public override void ProcessMetadata(GameObject gameObject, IncubatorMetadata metadata)
    {
        Incubator incubator = gameObject.GetComponentInChildren<Incubator>();
        bool hatchingNow = IsHatchingSeenHere(incubator.powered, incubator.hatched, metadata.Hatched);

        if (metadata.Powered && !incubator.powered)
        {
            RestorePowered(gameObject, incubator);
        }

        if (metadata.Hatched && !incubator.hatched)
        {
            incubator.hatched = true;
            if (hatchingNow)
            {
                incubator.OnHatched();
            }
            else
            {
                RestoreHatched(incubator);
            }
        }
    }

    /// <summary>
    ///     A player who already sees the incubator powered and waiting for the enzymes watches somebody hatch the eggs. A
    ///     player for whom the incubator has just spawned is only given its state.
    /// </summary>
    internal static bool IsHatchingSeenHere(bool shownPowered, bool shownHatched, bool hatched)
    {
        return hatched && shownPowered && !shownHatched;
    }

    private static void RestorePowered(GameObject gameObject, Incubator incubator)
    {
        // Incubator.SetPowered without its power-up sound
        incubator.powered = true;
        incubator.computerTerminal.SetActive(incubator.terminalActive);
        SetEmissive(incubator, 1f);

        // IncubatorActivationTerminal.CloseDeck without its sound
        IncubatorActivationTerminal terminal = gameObject.GetComponentInChildren<IncubatorActivationTerminal>();
        if (terminal)
        {
            terminal.animator.SetBool("Open", false);
        }
    }

    private static void RestoreHatched(Incubator incubator)
    {
        // What Incubator.Start does for a hatched incubator. Its eggs only exist once Start has run, and then it is up to us.
        for (int i = 0; i < incubator.eggs.Count; i++)
        {
            incubator.eggs[i].animationController.SetHatched(incubator.hatchAnimations[i]);
        }
        incubator.computerTerminal.SetActive(incubator.terminalActive);
        SetEmissive(incubator, 0f);
    }

    private static void SetEmissive(Incubator incubator, float intensity)
    {
        incubator.emissiveIntensity = intensity;
        incubator.UpdateMaterialsEmissive();
        incubator.terminalLight.intensity = intensity * LIGHT_INTENSITY_PER_EMISSIVE;
    }
}
