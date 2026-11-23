using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <remarks>
///     The service is looked up when it is needed, see <see cref="MapRoomCameraMetadataProcessor" />.
/// </remarks>
public class PictureFrameMetadataProcessor : EntityMetadataProcessor<PictureFrameMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, PictureFrameMetadata metadata)
    {
        PictureFrame frame = gameObject.GetComponentInChildren<PictureFrame>(true);
        if (!frame)
        {
            Log.Error($"Could not find PictureFrame on {gameObject.name}");
            return;
        }
        Resolve<PictureFrames>().ShowSharedPicture(frame, metadata.PictureHash);
    }
}
