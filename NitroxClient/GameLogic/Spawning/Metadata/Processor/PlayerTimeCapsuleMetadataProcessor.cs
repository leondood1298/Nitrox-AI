using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class PlayerTimeCapsuleMetadataProcessor : EntityMetadataProcessor<PlayerTimeCapsuleMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, PlayerTimeCapsuleMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out PlayerTimeCapsule capsule))
        {
            Log.Error($"[{nameof(PlayerTimeCapsuleMetadataProcessor)}] Could not find a PlayerTimeCapsule on {gameObject.name}");
            return;
        }

        // Stored first: the tab's field below tells the game the text again, which is then no change to send back
        capsule.text = metadata.Text;
        capsule.title = metadata.Title;

        // The open tab shows the text it read when it opened
        uGUI_PDA pda = uGUI_PDA.main;
        if (pda && pda.GetTab(PDATab.TimeCapsule) is uGUI_TimeCapsuleTab tab && tab.gameObject.activeSelf && tab.inputFieldText.text != metadata.Text)
        {
            tab.inputFieldText.text = metadata.Text;
        }
    }
}
