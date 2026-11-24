using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic;

/// <summary>
///     The text, colour and look of a sign or of a locker's label are the metadata of the sign or locker. The player who
///     edits one sends it when a colour is picked (so the others see it at once) and when they leave the label.
/// </summary>
public class SignLabels(Entities entities)
{
    private readonly Entities entities = entities;

    public void BroadcastState(uGUI_SignInput sign)
    {
        PrefabIdentifier parentIdentifier = sign.gameObject.FindAncestor<PrefabIdentifier>();
        if (!parentIdentifier || !parentIdentifier.TryGetIdOrWarn(out NitroxId id))
        {
            return;
        }
        EntitySignMetadata metadata = new(sign.text, sign.colorIndex, sign.scaleIndex, sign.elementsState, sign.IsBackground());
        entities.BroadcastMetadataUpdate(id, metadata);
    }
}
